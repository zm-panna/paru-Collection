using Ecommerce.Application;
using Ecommerce.Domain;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class FulfilmentService(AppDb db,StockService stock){
 public async Task ReserveAsync(Order order,OrderLine line,string actor){
  var split=order.Source!="POS"&&await new BusinessRulesService(db).SettingAsync("AllowSplitFulfillment","true")=="true";
  var branch=await db.Set<Warehouse>().Where(x=>x.Id==order.WarehouseId&&x.Active).Select(x=>x.BranchId).SingleAsync();
  var balances=await db.Set<StockBalance>().Where(x=>x.SkuId==line.SkuId&&x.Warehouse.Active&&(x.WarehouseId==order.WarehouseId||(split&&x.Warehouse.BranchId==branch))).OrderByDescending(x=>x.WarehouseId==order.WarehouseId).ThenBy(x=>x.WarehouseId).ToListAsync();
  if(balances.Sum(x=>x.OnHand-x.Reserved)<line.Quantity)throw new BusinessException("Insufficient available stock for "+line.Name);
  var remaining=line.Quantity;foreach(var b in balances){var quantity=Math.Min(remaining,b.OnHand-b.Reserved);if(quantity==0)continue;await stock.ChangeAsync(b.WarehouseId,line.SkuId,0,quantity,"Reserve",order.Number,actor);db.Add(new OrderAllocation{OrderLineId=line.Id,WarehouseId=b.WarehouseId,Quantity=quantity});remaining-=quantity;if(remaining==0)break;}
 }
 public async Task IssueOrReleaseAsync(Order o,OrderLine line,bool issue,string actor){
  var allocations=await db.Set<OrderAllocation>().Where(x=>x.OrderLineId==line.Id).OrderBy(x=>x.WarehouseId).ToListAsync();
  if(allocations.Count==0)allocations.Add(new OrderAllocation{OrderLineId=line.Id,WarehouseId=o.WarehouseId,Quantity=line.Quantity}); // Existing orders before allocation migration.
  decimal cost=0;foreach(var a in allocations){if(issue)a.UnitCost=(await db.Set<StockBalance>().SingleAsync(x=>x.WarehouseId==a.WarehouseId&&x.SkuId==line.SkuId)).AverageCost;await stock.ChangeAsync(a.WarehouseId,line.SkuId,issue?-a.Quantity:0,-a.Quantity,issue?"Shipped":"Cancelled",o.Number,actor,a.UnitCost);cost+=a.UnitCost*a.Quantity;}if(issue)line.Cost=Math.Round(cost/line.Quantity,4,MidpointRounding.AwayFromZero);
 }
 public async Task RestockAsync(Order o,OrderLine line,int quantity,string actor){
  var allocations=await db.Set<OrderAllocation>().Where(x=>x.OrderLineId==line.Id).OrderBy(x=>x.WarehouseId).ToListAsync();if(allocations.Count==0){await stock.ChangeAsync(o.WarehouseId,line.SkuId,quantity,0,"SalesReturn",o.Number,actor,line.Cost);return;}
  foreach(var a in allocations){var q=Math.Min(quantity,a.Quantity-a.RestockedQuantity);if(q<=0)continue;await stock.ChangeAsync(a.WarehouseId,line.SkuId,q,0,"SalesReturn",o.Number,actor,line.Cost);a.RestockedQuantity+=q;quantity-=q;if(quantity==0)return;}throw new BusinessException("Return exceeds fulfilled quantity.");
 }
}
