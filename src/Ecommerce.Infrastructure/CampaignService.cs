using Ecommerce.Domain;
using Ecommerce.Application;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public record CampaignPrice(decimal Price,Guid? CampaignId,decimal Discount);
// Preview and checkout share the same basket pricing. Checkout calls inside its serializable transaction.
public class CampaignService(AppDb db){
 public async Task<Dictionary<Guid,CampaignPrice>> QuoteAsync(List<CartItem> cart,string user){
  var now=DateTime.UtcNow;var subtotal=cart.Sum(x=>x.Sku.SalePrice*x.Quantity);
  var campaigns=await db.Set<Campaign>().Where(x=>x.Active&&x.StartsAt<=now&&x.EndsAt>now&&(x.CustomerId==""||x.CustomerId==user)&&x.MinimumPurchase<=subtotal).OrderByDescending(x=>x.Percent).ThenBy(x=>x.Id).ToListAsync();
  var eligible=new List<Campaign>();foreach(var c in campaigns)if(await db.Set<CampaignUse>().CountAsync(x=>x.CampaignId==c.Id)<c.UsageLimit&&await db.Set<CampaignUse>().CountAsync(x=>x.CampaignId==c.Id&&x.UserId==user)<c.PerCustomerLimit)eligible.Add(c);
  var remaining=eligible.ToDictionary(x=>x.Id,x=>x.MaximumDiscount);var result=new Dictionary<Guid,CampaignPrice>();
  foreach(var line in cart.OrderBy(x=>x.SkuId)){var sku=line.Sku;var best=new CampaignPrice(sku.SalePrice,null,0);foreach(var c in eligible.Where(x=>(x.ProductId==null||x.ProductId==sku.ProductId)&&(x.CategoryId==null||x.CategoryId==sku.Product.CategoryId))){var perUnit=Math.Floor(Math.Min(sku.SalePrice*c.Percent/100,remaining[c.Id]/line.Quantity)*100)/100;var price=CommerceRules.Money(sku.SalePrice-perUnit);if(price<best.Price)best=new(price,c.Id,perUnit*line.Quantity);}if(best.CampaignId.HasValue)remaining[best.CampaignId.Value]-=best.Discount;result.Add(line.SkuId,best);}
  return result;
 }
}
