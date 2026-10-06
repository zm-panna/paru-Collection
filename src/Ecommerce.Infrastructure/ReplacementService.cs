using System.Data;
using Ecommerce.Application;
using Ecommerce.Domain;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
// Same-SKU, same-quantity replacement funded by an accepted paid return.
public class ReplacementService(AppDb db,StockService stock,LedgerService ledger){
 public async Task<Guid> CreateAsync(Guid returnId,string actor,Guid? targetSkuId=null,int? targetQuantity=null,decimal collectedDifference=0,string collectionReference=""){
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var prior=await db.Set<Replacement>().SingleOrDefaultAsync(x=>x.ReturnRequestId==returnId);if(prior!=null)return prior.OrderId;
  var r=await db.Set<ReturnRequest>().Include(x=>x.OrderLine).Include(x=>x.Order).SingleAsync(x=>x.Id==returnId);
  if(r.Status!="Accepted"||r.Order.PaymentStatus!="Paid")throw new BusinessException("Replacement requires an accepted, paid return.");
  var refund=await db.Set<PaymentRefund>().SingleOrDefaultAsync(x=>x.OrderId==r.OrderId&&x.Reason=="Return "+r.Id);
  if(refund==null||refund.Status!="Requested")throw new BusinessException("Return refund is already processed or unavailable.");
  var requestedSku=targetSkuId??r.OrderLine.SkuId;var quantity=targetQuantity??r.Quantity;CommerceRules.Quantity(quantity);var sku=await db.Set<ProductSku>().Include(x=>x.Product).SingleAsync(x=>x.Id==requestedSku);if(!sku.Active||!sku.Product.Active)throw new BusinessException("Replacement SKU is unavailable; use refund or credit.");
  var tax=CommerceRules.Money(r.OrderLine.Tax*r.Quantity/r.OrderLine.Quantity);var net=refund.Amount-tax;
  var o=new Order{Number="REP-"+Guid.NewGuid().ToString("N")[..20],UserId=r.Order.UserId,WarehouseId=r.Order.WarehouseId,CustomerName=r.Order.CustomerName,Phone=r.Order.Phone,Email=r.Order.Email,ShippingAddress=r.Order.ShippingAddress,Source="Replacement",PaymentMethod="ReplacementCredit",PaymentStatus="Paid",Subtotal=net,Tax=tax,Total=refund.Amount,CheckoutKey=Guid.NewGuid().ToString()};
  o.Lines.Add(new OrderLine{SkuId=sku.Id,Name=sku.Product.Name+" / "+sku.Name,Quantity=r.Quantity,Price=r.OrderLine.Price,Tax=tax});o.Subtotal=r.OrderLine.Price*r.Quantity;o.Discount=o.Subtotal-net;
  if(sku.Id!=r.OrderLine.SkuId||quantity!=r.Quantity){var price=sku.SalePrice;o.Lines.Clear();tax=CommerceRules.Money(price*quantity*sku.VatPercent/100);o.Lines.Add(new OrderLine{SkuId=sku.Id,Name=sku.Product.Name+" / "+sku.Name,Quantity=quantity,Price=price,Tax=tax});o.Subtotal=price*quantity;o.Discount=0;o.Tax=tax;o.Total=o.Subtotal+tax;}
  var difference=o.Total-refund.Amount;
  if(difference>0){if(collectedDifference!=difference||string.IsNullOrWhiteSpace(collectionReference)||collectionReference.Length>100)throw new BusinessException("Collect the exact exchange difference of "+difference+" in cash and enter its receipt reference.");if(await db.Set<PaymentTransaction>().AnyAsync(x=>x.GatewayTransactionId==collectionReference))throw new BusinessException("Receipt reference already used.");await ledger.PostAsync("EXCHANGE-CASH-"+o.Id,"Exchange cash difference",("1000",difference,0,""),("1100",0,difference,o.UserId));}
  else if(collectedDifference!=0)throw new BusinessException("No cash difference is due.");
  if(difference<0){db.Add(new StoreCredit{UserId=o.UserId,PaymentRefundId=refund.Id,Amount=-difference,Reason="Exchange residual credit "+o.Number});await ledger.PostAsync("EXCHANGE-CREDIT-"+o.Id,"Exchange residual credit",("1100",-difference,0,o.UserId),("2200",0,-difference,o.UserId));}
  db.Add(new PaymentTransaction{OrderId=o.Id,TransactionId="REPLACEMENT-"+o.Id.ToString("N"),Gateway="ReplacementCredit",GatewayTransactionId=difference>0?collectionReference:"",Amount=o.Total,Status="Paid",VerificationStatus="AcceptedReturnAndStaffCash",VerifiedAt=DateTime.UtcNow});
  o.History.Add(new OrderHistory{Status="Pending",ActorId=actor,Note="Replacement for "+r.Order.Number});
  await new FulfilmentService(db,stock).ReserveAsync(o,o.Lines[0],actor);db.Add(o);db.Add(new Replacement{ReturnRequestId=r.Id,OrderId=o.Id,ActorId=actor});refund.Status="ReplacementIssued";refund.ExternalReference=o.Number;
  // Credit already exists on the same customer's receivable from return accounting.
  // New delivery debits it again; no additional payment/receivable journal here.
  ledger.Audit(actor,"ReplacementCreated",o,after:r.Id.ToString());ledger.Notify(o.UserId,"Replacement order "+o.Number+" created.");await db.SaveChangesAsync();await tx.CommitAsync();return o.Id;
 }
}
