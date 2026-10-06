using System.Data;
using Ecommerce.Domain;
using Ecommerce.Application;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class ReturnService(AppDb db,StockService stock,LedgerService ledger){
 public async Task RequestAsync(Guid lineId,int qty,string reason,string user){
  CommerceRules.Quantity(qty);await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var l=await db.Set<OrderLine>().Include(x=>x.Order).SingleOrDefaultAsync(x=>x.Id==lineId&&x.Order.UserId==user)??throw new BusinessException("Order line not found.");
  var setting=await new BusinessRulesService(db).SettingAsync("ReturnDays","7");var days=int.TryParse(setting,out var parsed)?Math.Clamp(parsed,0,365):7;var delivery=await db.Set<OrderHistory>().Where(x=>x.OrderId==l.OrderId&&x.Status=="Delivered").OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync();if(delivery==null||DateTime.UtcNow>delivery.CreatedAt.AddDays(days))throw new BusinessException("Return window has expired.");
  var taken=await db.Set<ReturnRequest>().Where(x=>x.OrderLineId==lineId&&x.Status!="Rejected").SumAsync(x=>(int?)x.Quantity)??0;
  if(l.Order.Status!=OrderStatus.Delivered||qty+taken>l.Quantity||string.IsNullOrWhiteSpace(reason))throw new BusinessException("Return quantity or status is invalid.");
  db.Add(new ReturnRequest{OrderId=l.OrderId,OrderLineId=lineId,Quantity=qty,Reason=reason});await db.SaveChangesAsync();await tx.CommitAsync();
 }
 public async Task InspectAsync(Guid id,bool accept,bool restock,string note,string actor){
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var r=await db.Set<ReturnRequest>().Include(x=>x.OrderLine).Include(x=>x.Order).SingleAsync(x=>x.Id==id);
  if(r.Status!="Requested")throw new BusinessException("Return already reviewed.");if(string.IsNullOrWhiteSpace(note))throw new BusinessException("Inspection notes are required.");
  r.Status=accept?"Accepted":"Rejected";r.Restock=accept&&restock;r.Inspection=note;
  if(accept){var o=r.Order;var l=r.OrderLine;var gross=l.Price*r.Quantity;var discount=o.Subtotal==0?0:o.Discount*gross/o.Subtotal;var tax=CommerceRules.Money(l.Tax*r.Quantity/l.Quantity);var net=CommerceRules.Money(gross-discount);var amount=net+tax;
   if(restock)await new FulfilmentService(db,stock).RestockAsync(o,l,r.Quantity,actor);
   var entries=new List<(string,decimal,decimal,string)>{("4200",net,0,""),("2100",tax,0,""),("1100",0,amount,o.UserId)};
   if(restock&&l.Cost>0){var returnCost=CommerceRules.Money(l.Cost*r.Quantity);entries.Add(("1200",returnCost,0,""));entries.Add(("5000",0,returnCost,""));}
   if(amount>0)await ledger.PostAsync("RET-"+r.Id,"Accepted sales return",entries.ToArray());
   if(o.PaymentStatus=="Paid"&&amount>0)db.Add(new PaymentRefund{OrderId=o.Id,Amount=amount,Reason="Return "+r.Id});
  }
  await db.SaveChangesAsync();await RefreshStatusAsync(r.OrderId,actor);
  ledger.Audit(actor,"ReturnInspection",r,after:r.Status+": "+note);await db.SaveChangesAsync();await tx.CommitAsync();
 }
 // Records a refund completed externally; does not pretend to call a provider API.
 public async Task RecordRefundAsync(Guid id,string externalReference,string actor){
  if(string.IsNullOrWhiteSpace(externalReference))throw new BusinessException("Verified external refund reference is required.");
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var r=await db.Set<PaymentRefund>().Include(x=>x.Order).SingleAsync(x=>x.Id==id);
  if(r.Status=="Completed")return;if(r.Status is "StoreCreditIssued" or "ReplacementIssued")throw new BusinessException("This refund was converted to store credit.");var paid=(await db.Set<PaymentTransaction>().Where(x=>x.OrderId==r.OrderId&&x.Status=="Paid").Select(x=>x.Amount).ToListAsync()).Sum();
  var refunded=(await db.Set<PaymentRefund>().Where(x=>x.OrderId==r.OrderId&&x.Status=="Completed").Select(x=>x.Amount).ToListAsync()).Sum();
  if(r.Amount<=0||r.Amount+refunded>paid)throw new BusinessException("Refund exceeds collected amount.");
  r.Status="Completed";r.ExternalReference=externalReference;
  await ledger.PostAsync("REFUND-"+r.Id,"Externally completed refund",("1100",r.Amount,0,r.Order.UserId),(r.Order.PaymentMethod=="COD"?"1000":"1010",0,r.Amount,""));
  await db.SaveChangesAsync();await RefreshStatusAsync(r.OrderId,actor);
  ledger.Audit(actor,"RefundRecorded",r,after:externalReference);ledger.Notify(r.Order.UserId,"Refund recorded for "+r.Order.Number);await db.SaveChangesAsync();await tx.CommitAsync();
 }
 public async Task RefreshStatusAsync(Guid orderId,string actor){
  var o=await db.Set<Order>().Include(x=>x.Lines).SingleAsync(x=>x.Id==orderId);if(o.Lines.Count==0||o.Status is not (OrderStatus.Delivered or OrderStatus.Returned or OrderStatus.Refunded))return;
  var accepted=await db.Set<ReturnRequest>().Where(x=>x.OrderId==orderId&&x.Status=="Accepted").ToListAsync();if(!o.Lines.All(l=>accepted.Where(x=>x.OrderLineId==l.Id).Sum(x=>x.Quantity)==l.Quantity))return;
  var refunds=await db.Set<PaymentRefund>().Where(x=>x.OrderId==orderId).ToListAsync();var next=refunds.Count>0&&refunds.All(x=>x.Status is "Completed" or "StoreCreditIssued")?OrderStatus.Refunded:OrderStatus.Returned;if(next==o.Status)return;o.Status=next;db.Add(new OrderHistory{OrderId=o.Id,Status=next.ToString(),ActorId=actor,Note="All order items returned; shipping charge retained."});
 }
}
