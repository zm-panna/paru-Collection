using System.Data;
using Ecommerce.Domain;
using Ecommerce.Application;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class OrderService(AppDb db,StockService stock,LedgerService ledger){
 public Task<Order> CheckoutAsync(CheckoutRequest r)=>SqlRetry.RunAsync(db,()=>CheckoutCoreAsync(r));
 async Task<Order> CheckoutCoreAsync(CheckoutRequest r){
  if(string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Phone)||string.IsNullOrWhiteSpace(r.Address)||!Guid.TryParse(r.Key,out _))throw new BusinessException("Complete the delivery information.");
  if(r.Method is not ("COD" or "SSLCommerz" or "StoreCredit"))throw new BusinessException("Payment method unavailable.");
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var prior=await db.Set<Order>().SingleOrDefaultAsync(x=>x.UserId==r.UserId&&x.CheckoutKey==r.Key);if(prior!=null)return prior;
  var cart=await db.Set<CartItem>().Include(x=>x.Sku).ThenInclude(x=>x.Product).Where(x=>x.Owner==(r.CartOwner??r.UserId)).OrderBy(x=>x.SkuId).ToListAsync();
  if(cart.Count==0)throw new BusinessException("Your cart is empty.");
  if(!await db.Set<Warehouse>().AnyAsync(x=>x.Id==r.WarehouseId&&x.Active))throw new BusinessException("Warehouse unavailable.");
  var zone=await db.Set<DeliveryZone>().SingleOrDefaultAsync(x=>x.Id==r.ZoneId&&x.Active)??throw new BusinessException("Select delivery zone.");
  var o=new Order{Number="ORD-"+DateTime.UtcNow.ToString("yyyyMMdd")+"-"+Guid.NewGuid().ToString("N")[..12],UserId=r.UserId,CustomerName=r.Name,Phone=r.Phone,Email=r.Email,ShippingAddress=r.Address,WarehouseId=r.WarehouseId,PaymentMethod=r.Method,CheckoutKey=r.Key,Source=r.Source};
  var campaignPrices=await new CampaignService(db).QuoteAsync(cart,r.UserId);
  foreach(var c in cart){CommerceRules.Quantity(c.Quantity);if(!c.Sku.Active||!c.Sku.Product.Active)throw new BusinessException("A product is unavailable.");
   o.Lines.Add(new OrderLine{SkuId=c.SkuId,Name=c.Sku.Product.Name+" / "+c.Sku.Name,Quantity=c.Quantity,Price=campaignPrices[c.SkuId].Price,Cost=c.Sku.PurchasePrice});
   await new FulfilmentService(db,stock).ReserveAsync(o,o.Lines.Last(),r.UserId);
  }
  o.Subtotal=o.Lines.Sum(x=>x.Price*x.Quantity);Coupon? coupon=null;
  if(!string.IsNullOrWhiteSpace(r.Coupon)){coupon=await db.Set<Coupon>().SingleOrDefaultAsync(x=>x.Code==r.Coupon.Trim())??throw new BusinessException("Coupon not found.");o.Discount=CommerceRules.Discount(coupon,o.Subtotal,await db.Set<CouponUse>().CountAsync(x=>x.CouponId==coupon.Id),await db.Set<CouponUse>().CountAsync(x=>x.CouponId==coupon.Id&&x.UserId==r.UserId),DateTime.UtcNow);}
  foreach(var l in o.Lines){var vat=cart.Single(x=>x.SkuId==l.SkuId).Sku.VatPercent;var allocated=o.Subtotal==0?0:o.Discount*(l.Price*l.Quantity/o.Subtotal);l.Tax=CommerceRules.Money((l.Price*l.Quantity-allocated)*vat/100);}
  o.Tax=o.Lines.Sum(x=>x.Tax);o.Shipping=zone.FreeAbove.HasValue&&o.Subtotal-o.Discount>=zone.FreeAbove?0:zone.Charge;o.Total=o.Subtotal-o.Discount+o.Tax+o.Shipping;
  o.History.Add(new OrderHistory{Status="Pending",ActorId=r.UserId,Note="Order placed; stock reserved"});db.Add(o);
  foreach(var group in campaignPrices.Values.Where(x=>x.CampaignId.HasValue).GroupBy(x=>x.CampaignId!.Value))db.Add(new CampaignUse{CampaignId=group.Key,OrderId=o.Id,UserId=r.UserId,Discount=group.Sum(x=>x.Discount)});
  if(coupon!=null)db.Add(new CouponUse{CouponId=coupon.Id,OrderId=o.Id,UserId=r.UserId});
  if(r.Method=="StoreCredit")await new StoreCreditService(db,ledger).RedeemAsync(o);
  db.RemoveRange(cart);ledger.Audit(r.UserId,"Checkout",o,after:o.Total.ToString());ledger.Notify(r.UserId,"Order "+o.Number+" placed.");await db.SaveChangesAsync();await tx.CommitAsync();return o;
 }
 public async Task TransitionAsync(Guid id,OrderStatus next,string actor){
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var o=await db.Set<Order>().Include(x=>x.Lines).SingleAsync(x=>x.Id==id);
  if(o.Status==next)return;if(!CommerceRules.CanTransition(o.Status,next))throw new BusinessException("Invalid order status transition.");
  if(next==OrderStatus.Cancelled&&o.PaymentStatus=="Paid")throw new BusinessException("Paid order cancellation requires a reviewed refund; do not cancel directly.");
  if(next==OrderStatus.Shipped&&o.PaymentMethod!="COD"&&o.PaymentStatus!="Paid")throw new BusinessException("Online payment is not verified.");
  if(next is OrderStatus.Cancelled or OrderStatus.Shipped)foreach(var l in o.Lines.OrderBy(x=>x.SkuId))await new FulfilmentService(db,stock).IssueOrReleaseAsync(o,l,next==OrderStatus.Shipped,actor);
  if(next==OrderStatus.Delivered){var cost=CommerceRules.Money(o.Lines.Sum(x=>x.Quantity*x.Cost));var lines=new List<(string,decimal,decimal,string)>{("1100",o.Total,0,o.UserId),("4000",0,o.Subtotal-o.Discount,""),("2100",0,o.Tax,""),("4100",0,o.Shipping,"")};if(cost>0){lines.Add(("5000",cost,0,""));lines.Add(("1200",0,cost,""));}await ledger.PostAsync("SALE-"+o.Number,"Delivered sale",lines.ToArray());}
  var old=o.Status;o.Status=next;db.Add(new OrderHistory{OrderId=o.Id,Status=next.ToString(),ActorId=actor});ledger.Audit(actor,"OrderStatus",o,old.ToString(),next.ToString());ledger.Notify(o.UserId,o.Number+": "+next);await db.SaveChangesAsync();await tx.CommitAsync();
 }
 public async Task<decimal> CodDueAsync(Order o){var returns=await db.Set<ReturnRequest>().Include(x=>x.OrderLine).Where(x=>x.OrderId==o.Id&&x.Status=="Accepted").ToListAsync();var credited=returns.Sum(r=>CommerceRules.Money(r.OrderLine.Price*r.Quantity-(o.Subtotal==0?0:o.Discount*r.OrderLine.Price*r.Quantity/o.Subtotal))+CommerceRules.Money(r.OrderLine.Tax*r.Quantity/r.OrderLine.Quantity));return Math.Max(0,o.Total-credited);}
 public async Task CollectCodAsync(Guid id,string actor,decimal? expectedAmount=null){
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var o=await db.Set<Order>().SingleAsync(x=>x.Id==id);
  if(o.PaymentStatus=="Paid")return;if(o.PaymentMethod!="COD"||o.Status is not (OrderStatus.Delivered or OrderStatus.Returned))throw new BusinessException("Only delivered COD orders can be collected.");
  var amount=await CodDueAsync(o);if(expectedAmount.HasValue&&expectedAmount!=amount)throw new BusinessException("COD amount changed after returns. Reload and verify the cash amount.");o.PaymentStatus="Paid";
  if(amount>0){db.Add(new PaymentTransaction{OrderId=id,TransactionId="COD-"+o.Number,Gateway="COD",Amount=amount,Status="Paid",VerificationStatus="StaffCollection",VerifiedAt=DateTime.UtcNow});await ledger.PostAsync("PAY-"+o.Number,"COD collection after accepted returns",("1000",amount,0,""),("1100",0,amount,o.UserId));}
  ledger.Audit(actor,"CODCollection",o,after:amount.ToString());await db.SaveChangesAsync();await tx.CommitAsync();
 }
 public async Task CollectTerminalAsync(Guid id,string method,string reference,string actor){
  if(method is not ("Card" or "MFS")||string.IsNullOrWhiteSpace(reference)||reference.Length>100)throw new BusinessException("Choose Card/MFS and enter the verified terminal reference.");
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var o=await db.Set<Order>().SingleAsync(x=>x.Id==id);if(o.Source!="POS")throw new BusinessException("Terminal reconciliation is POS-only.");if(o.PaymentStatus=="Paid")return;
  if(o.Status==OrderStatus.Cancelled)throw new BusinessException("Order was cancelled.");if(await db.Set<PaymentTransaction>().AnyAsync(x=>x.GatewayTransactionId==reference))throw new BusinessException("Terminal reference already recorded.");
  o.PaymentMethod=method;o.PaymentStatus="Paid";db.Add(new PaymentTransaction{OrderId=id,TransactionId="POS-"+o.Id.ToString("N"),Gateway="Terminal"+method,GatewayTransactionId=reference,Amount=o.Total,Status="Paid",VerificationStatus="StaffVerifiedTerminal",VerifiedAt=DateTime.UtcNow});await ledger.PostAsync("PAY-"+o.Number,"Verified POS terminal collection",("1010",o.Total,0,""),("1100",0,o.Total,o.UserId));ledger.Audit(actor,"TerminalPaymentRecorded",o,after:reference);await db.SaveChangesAsync();await tx.CommitAsync();
 }

}
