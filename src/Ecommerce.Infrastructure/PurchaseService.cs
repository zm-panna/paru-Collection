using System.Data;
using Ecommerce.Domain;
using Ecommerce.Application;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class PurchaseService(AppDb db,StockService stock,LedgerService ledger){
 public async Task<PurchaseOrder> CreateAsync(Guid supplier,Guid warehouse,Guid[] skus,int[] quantities,decimal[] costs,string actor){
  if(skus.Length==0||skus.Length!=quantities.Length||skus.Length!=costs.Length||skus.Distinct().Count()!=skus.Length)throw new BusinessException("Enter distinct purchase lines.");
  var p=new PurchaseOrder{Number="PO-"+Guid.NewGuid().ToString("N")[..16],SupplierId=supplier,WarehouseId=warehouse};
  for(int i=0;i<skus.Length;i++){CommerceRules.Quantity(quantities[i]);if(costs[i]<0||costs[i]!=CommerceRules.Money(costs[i]))throw new BusinessException("Invalid purchase cost.");p.Lines.Add(new PurchaseLine{SkuId=skus[i],Quantity=quantities[i],UnitCost=costs[i]});}
  p.Total=p.Lines.Sum(x=>x.Quantity*x.UnitCost);db.Add(p);ledger.Audit(actor,"PurchaseCreated",p);await db.SaveChangesAsync();return p;
 }
 public async Task ApproveAsync(Guid id,string actor){var p=await db.Set<PurchaseOrder>().SingleAsync(x=>x.Id==id);if(p.Status!="Draft")throw new BusinessException("Only draft purchases can be approved.");p.Status="Approved";ledger.Audit(actor,"PurchaseApproved",p);await db.SaveChangesAsync();}
 public async Task ReceiveAsync(Guid id,string actor){var p=await db.Set<PurchaseOrder>().Include(x=>x.Lines).SingleAsync(x=>x.Id==id);if(p.Status=="Received")return;await new ProcurementService(db,stock,ledger).ReceiveAsync(id,p.Lines.Select(x=>x.Id).ToArray(),p.Lines.Select(x=>x.Quantity-x.ReceivedQuantity).ToArray(),"FULL-"+p.Number,actor);}
 public async Task PayAsync(Guid id,decimal amount,string actor){await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var p=await db.Set<PurchaseOrder>().SingleAsync(x=>x.Id==id);if(p.Status is not ("Received" or "PartReceived")||amount!=CommerceRules.Money(amount)||amount<=0||p.Paid+amount>p.ReceivedTotal-p.ReturnedTotal)throw new BusinessException("Payment exceeds supplier due or purchase is not received.");var approved=(await db.Set<SupplierInvoice>().Where(x=>x.PurchaseOrderId==id&&x.Status=="Approved").Select(x=>x.Total).ToListAsync()).Sum();if(p.Paid+amount>approved-p.ReturnedTotal)throw new BusinessException("Approve a matched supplier invoice before payment.");p.Paid+=amount;await ledger.PostAsync("SP-"+Guid.NewGuid().ToString("N"),"Supplier payment",("2000",amount,0,p.SupplierId.ToString()),("1000",0,amount,""));ledger.Audit(actor,"SupplierPayment",p,after:amount.ToString());await db.SaveChangesAsync();await tx.CommitAsync();}
}
