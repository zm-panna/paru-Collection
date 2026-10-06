using Ecommerce.Domain;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class BusinessRulesService(AppDb db){
 public async Task<string> SettingAsync(string name,string fallback){return await db.Set<BusinessSetting>().Where(x=>x.Active&&x.Name==name).Select(x=>x.Value).FirstOrDefaultAsync()??fallback;}
 public async Task<decimal> PriceAsync(ProductSku sku,string user){sku.Product??=await db.Set<Product>().SingleAsync(x=>x.Id==sku.ProductId);return (await new CampaignService(db).QuoteAsync([new CartItem{SkuId=sku.Id,Sku=sku,Quantity=1}],user))[sku.Id].Price;}
 public async Task<Guid> WarehouseAsync(){var value=await SettingAsync("DefaultWarehouseId","");if(Guid.TryParse(value,out var id)&&await db.Set<Warehouse>().AnyAsync(x=>x.Id==id&&x.Active))return id;return await db.Set<Warehouse>().Where(x=>x.Active).OrderBy(x=>x.CreatedAt).Select(x=>x.Id).FirstAsync();}
}
