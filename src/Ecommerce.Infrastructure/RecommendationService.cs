using Ecommerce.Domain;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class RecommendationService(AppDb db){
 public async Task<List<Product>> ForAsync(string? userId,int take=8){
  var categories=new List<Guid>();var bought=new List<Guid>();
  if(!string.IsNullOrWhiteSpace(userId)){
   categories=await db.Set<ProductView>().Where(x=>x.UserId==userId).OrderByDescending(x=>x.CreatedAt).Take(30).Select(x=>x.Product.CategoryId).ToListAsync();
   var purchases=await db.Set<OrderLine>().Where(x=>x.Order.UserId==userId&&(x.Order.Status==OrderStatus.Delivered||x.Order.Status==OrderStatus.Returned||x.Order.Status==OrderStatus.Refunded)).Select(x=>new{x.Sku.ProductId,x.Sku.Product.CategoryId}).ToListAsync();categories.AddRange(purchases.Select(x=>x.CategoryId));bought=purchases.Select(x=>x.ProductId).ToList();
  }
  var preferred=categories.Distinct().ToArray();var popularity=await db.Set<OrderLine>().Where(x=>(x.Order.Status==OrderStatus.Delivered||x.Order.Status==OrderStatus.Returned||x.Order.Status==OrderStatus.Refunded)).GroupBy(x=>x.Sku.ProductId).Select(g=>new{Id=g.Key,Qty=g.Sum(x=>x.Quantity)}).ToDictionaryAsync(x=>x.Id,x=>x.Qty);
  var candidates=await db.Set<Product>().AsNoTracking().Include(x=>x.Skus).Include(x=>x.Files).Where(x=>x.Active&&!bought.Contains(x.Id)).OrderByDescending(x=>preferred.Contains(x.CategoryId)).ThenByDescending(x=>x.Featured).ThenByDescending(x=>x.CreatedAt).Take(100).ToListAsync();
  return candidates.OrderByDescending(x=>preferred.Contains(x.CategoryId)).ThenByDescending(x=>popularity.GetValueOrDefault(x.Id)).ThenByDescending(x=>x.Featured).Take(Math.Clamp(take,1,20)).ToList();
 }
}
