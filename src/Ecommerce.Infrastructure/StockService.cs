using System.Data;
using Ecommerce.Domain;
using Ecommerce.Application;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class StockService(AppDb db,LedgerService ledger) {
 // Call inside a serializable transaction. Rowversion additionally guards stale writes.
 public async Task ChangeAsync(Guid warehouse,Guid sku,int quantity,int reserve,string kind,string reference,string actor,decimal cost=0){
  var s=await db.Set<StockBalance>().SingleOrDefaultAsync(x=>x.WarehouseId==warehouse&&x.SkuId==sku);
  if(s==null){s=new StockBalance{WarehouseId=warehouse,SkuId=sku};db.Add(s);}
  if(s.OnHand+quantity<0||s.Reserved+reserve<0||s.OnHand+quantity<s.Reserved+reserve)throw new BusinessException("Insufficient available stock.");
  if(quantity<0)cost=s.AverageCost;
  if(quantity>0)s.AverageCost=Math.Round((s.OnHand*s.AverageCost+quantity*cost)/(s.OnHand+quantity),4,MidpointRounding.AwayFromZero);
  s.OnHand+=quantity;s.Reserved+=reserve;
  db.Add(new StockMovement{WarehouseId=warehouse,SkuId=sku,Quantity=quantity,ReservedChange=reserve,Kind=kind,Reference=reference,ActorId=actor,UnitCost=cost});
 }
 public async Task AdjustAsync(Guid warehouse,Guid sku,int qty,string reason,string actor){
  if(qty==0||Math.Abs((long)qty)>100000||string.IsNullOrWhiteSpace(reason))throw new BusinessException("Enter a valid quantity and reason.");
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var item=await db.Set<ProductSku>().SingleAsync(x=>x.Id==sku);var reference="ADJ-"+Guid.NewGuid().ToString("N");
  await ChangeAsync(warehouse,sku,qty,0,"Adjustment",reference,actor,item.PurchasePrice);
  var balance=db.Set<StockBalance>().Local.SingleOrDefault(x=>x.WarehouseId==warehouse&&x.SkuId==sku)??await db.Set<StockBalance>().SingleAsync(x=>x.WarehouseId==warehouse&&x.SkuId==sku);
  var value=CommerceRules.Money(Math.Abs(qty)*(qty<0?balance.AverageCost:item.PurchasePrice));
  if(value>0)await ledger.PostAsync(reference,reason,("1200",qty>0?value:0,qty<0?value:0,""),("3100",qty<0?value:0,qty>0?value:0,""));
  ledger.Audit(actor,"StockAdjustment",item,after:qty+": "+reason);await db.SaveChangesAsync();await tx.CommitAsync();
 }
 public async Task TransferAsync(Guid from,Guid to,Guid sku,int qty,string actor){
  CommerceRules.Quantity(qty);if(from==to)throw new BusinessException("Select different warehouses.");
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var r="TR-"+Guid.NewGuid().ToString("N");
  var cost=(await db.Set<StockBalance>().SingleOrDefaultAsync(x=>x.WarehouseId==from&&x.SkuId==sku))?.AverageCost??0;
  await ChangeAsync(from,sku,-qty,0,"TransferOut",r,actor,cost);await ChangeAsync(to,sku,qty,0,"TransferIn",r,actor,cost);await db.SaveChangesAsync();await tx.CommitAsync();
 }
}
