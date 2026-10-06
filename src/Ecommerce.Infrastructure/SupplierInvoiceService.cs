using System.Data;
using Ecommerce.Application;
using Ecommerce.Domain;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class SupplierInvoiceService(AppDb db,LedgerService ledger){
 public async Task<Guid> CreateAsync(Guid purchaseId,string number,DateTime invoiceDate,DateTime dueDate,Guid[] lineIds,int[] quantities,string actor){
  if(string.IsNullOrWhiteSpace(number)||number.Trim().Length>100||dueDate.Date<invoiceDate.Date||lineIds.Length==0||lineIds.Length!=quantities.Length||lineIds.Distinct().Count()!=lineIds.Length)throw new BusinessException("Enter an invoice number, dates and distinct received lines.");
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var p=await db.Set<PurchaseOrder>().Include(x=>x.Lines).SingleAsync(x=>x.Id==purchaseId);if(p.Status is not ("Received" or "PartReceived"))throw new BusinessException("Receive purchase goods before invoicing.");
  if(await db.Set<SupplierInvoice>().AnyAsync(x=>x.SupplierId==p.SupplierId&&x.Number==number.Trim()))throw new BusinessException("Supplier invoice number already recorded.");
  var invoice=new SupplierInvoice{PurchaseOrderId=p.Id,SupplierId=p.SupplierId,Number=number.Trim(),InvoiceDate=invoiceDate,DueDate=dueDate};
  for(int i=0;i<lineIds.Length;i++){if(quantities[i]==0)continue;CommerceRules.Quantity(quantities[i]);var line=p.Lines.SingleOrDefault(x=>x.Id==lineIds[i])??throw new BusinessException("Invoice line does not belong to purchase.");var billed=await db.Set<SupplierInvoiceLine>().Where(x=>x.PurchaseLineId==line.Id&&x.SupplierInvoice.Status!="Voided").SumAsync(x=>(int?)x.Quantity)??0;if(billed+quantities[i]>line.ReceivedQuantity)throw new BusinessException("Invoice quantity exceeds uninvoiced receipts.");invoice.Lines.Add(new SupplierInvoiceLine{PurchaseLineId=line.Id,Quantity=quantities[i],UnitCost=line.UnitCost});}
  if(invoice.Lines.Count==0)throw new BusinessException("Enter at least one invoice quantity.");invoice.Total=invoice.Lines.Sum(x=>x.Quantity*x.UnitCost);db.Add(invoice);ledger.Audit(actor,"SupplierInvoiceCreated",invoice);await db.SaveChangesAsync();await tx.CommitAsync();return invoice.Id;
 }
 public async Task ApproveAsync(Guid id,string actor){await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var i=await db.Set<SupplierInvoice>().SingleAsync(x=>x.Id==id);if(i.Status=="Approved")return;if(i.Status!="Draft")throw new BusinessException("Only draft invoices can be approved.");i.Status="Approved";ledger.Audit(actor,"SupplierInvoiceApproved",i);await db.SaveChangesAsync();await tx.CommitAsync();} // Receipt already posts inventory/AP; approval never double-posts.
 public async Task VoidAsync(Guid id,string actor){var i=await db.Set<SupplierInvoice>().SingleAsync(x=>x.Id==id);if(i.Status!="Draft")throw new BusinessException("Only draft invoices can be voided.");i.Status="Voided";ledger.Audit(actor,"SupplierInvoiceVoided",i);await db.SaveChangesAsync();}
}
