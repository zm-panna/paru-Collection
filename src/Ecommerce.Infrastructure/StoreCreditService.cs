using System.Data;
using Ecommerce.Application;
using Ecommerce.Domain;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class StoreCreditService(AppDb db,LedgerService ledger){
 public async Task<decimal> BalanceAsync(string user)=>(await db.Set<StoreCredit>().Where(x=>x.UserId==user).Select(x=>x.Amount).ToListAsync()).Sum();
 public async Task IssueAsync(Guid refundId,string actor){await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var r=await db.Set<PaymentRefund>().Include(x=>x.Order).SingleAsync(x=>x.Id==refundId);if(r.Status=="StoreCreditIssued")return;if(r.Status!="Requested")throw new BusinessException("Only an unsubmitted refund can become store credit.");db.Add(new StoreCredit{UserId=r.Order.UserId,PaymentRefundId=r.Id,Amount=r.Amount,Reason="Approved return credit"});await ledger.PostAsync("CREDIT-"+r.Id,"Refund converted to store credit",("1100",r.Amount,0,r.Order.UserId),("2200",0,r.Amount,r.Order.UserId));r.Status="StoreCreditIssued";ledger.Audit(actor,"StoreCreditIssued",r);await db.SaveChangesAsync();await new ReturnService(db,new StockService(db,ledger),ledger).RefreshStatusAsync(r.OrderId,actor);await db.SaveChangesAsync();await tx.CommitAsync();}
 public async Task RedeemAsync(Order order){if(await BalanceAsync(order.UserId)<order.Total)throw new BusinessException("Insufficient store credit.");db.Add(new StoreCredit{UserId=order.UserId,OrderId=order.Id,Amount=-order.Total,Reason="Order "+order.Number});order.PaymentStatus="Paid";db.Add(new PaymentTransaction{OrderId=order.Id,Gateway="StoreCredit",TransactionId="CREDIT-"+order.Id.ToString("N"),Amount=order.Total,Status="Paid",VerificationStatus="InternalLedger",VerifiedAt=DateTime.UtcNow});await ledger.PostAsync("PAY-"+order.Number,"Store credit redemption",("2200",order.Total,0,order.UserId),("1100",0,order.Total,order.UserId));}
}
