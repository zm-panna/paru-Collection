using Ecommerce.Application;
using Ecommerce.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
namespace Ecommerce.Infrastructure;
public class CartService(AppDb db){
 public async Task SetAsync(string owner,Guid sku,int qty,bool add=false){
  if(qty<0||qty>10000)throw new BusinessException("Invalid quantity.");
  if(!await db.Set<ProductSku>().AnyAsync(x=>x.Id==sku&&x.Active&&x.Product.Active))throw new BusinessException("Product unavailable.");
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var row=await db.Set<CartItem>().SingleOrDefaultAsync(x=>x.Owner==owner&&x.SkuId==sku);
  if(qty==0){if(row!=null)db.Remove(row);}else if(row==null)db.Add(new CartItem{Owner=owner,SkuId=sku,Quantity=qty});else{row.Quantity=add?Math.Min(10000,row.Quantity+qty):qty;}
  await db.SaveChangesAsync();await tx.CommitAsync();
 }
 public async Task MergeAsync(string guest,string user){if(guest==user)return;await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var rows=await db.Set<CartItem>().Where(x=>x.Owner==guest).ToListAsync();foreach(var r in rows){var target=await db.Set<CartItem>().SingleOrDefaultAsync(x=>x.Owner==user&&x.SkuId==r.SkuId);if(target==null)r.Owner=user;else{target.Quantity=Math.Min(10000,target.Quantity+r.Quantity);db.Remove(r);}}
  await db.SaveChangesAsync();await tx.CommitAsync();
 }
}
