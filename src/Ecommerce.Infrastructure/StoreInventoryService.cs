using Ecommerce.Domain;
using Ecommerce.Application;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class StoreInventoryService(AppDb db,LedgerService ledger){
 public async Task<Guid> CreateAsync(string name,Guid branchId,string actor){if(string.IsNullOrWhiteSpace(name)||name.Length>160||!await db.Set<Branch>().AnyAsync(x=>x.Id==branchId&&x.Active))throw new BusinessException("Enter a store name and active branch.");await using var tx=await db.Database.BeginTransactionAsync();var w=new Warehouse{Name="Store: "+name.Trim(),BranchId=branchId};var store=new Store{Name=name.Trim(),WarehouseId=w.Id};db.AddRange(w,store);ledger.Audit(actor,"StoreInventoryCreated",store);await db.SaveChangesAsync();await tx.CommitAsync();return store.Id;}
}
