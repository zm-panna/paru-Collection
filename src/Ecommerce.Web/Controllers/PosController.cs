using System.Security.Claims;
using Ecommerce.Application;
using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
[Route("admin/pos"),Permit("orders.create")]
public class PosController(AppDb db,CartService carts,OrderService orders):Controller {
 string Uid=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 [HttpGet("")]public async Task<IActionResult> Index(){ViewBag.Skus=await db.Set<ProductSku>().Where(x=>x.Active).Take(1000).ToListAsync();ViewBag.Warehouses=await db.Set<Warehouse>().Where(x=>x.Active).ToListAsync();ViewBag.Key=Guid.NewGuid().ToString();return View(await db.Set<CartItem>().Include(x=>x.Sku).Where(x=>x.Owner=="pos:"+Uid).ToListAsync());}
 [HttpPost("add")]public async Task<IActionResult> Add(Guid skuId,int quantity){await carts.SetAsync("pos:"+Uid,skuId,quantity,true);return RedirectToAction(nameof(Index));}
 [HttpPost("remove")]public async Task<IActionResult> Remove(Guid skuId){await carts.SetAsync("pos:"+Uid,skuId,0);return RedirectToAction(nameof(Index));}
 [HttpPost("sale"),Permit("payments.edit"),Permit("orders.edit")]
 public async Task<IActionResult> Sale(string name,string phone,string key,string coupon="",string method="Cash",string reference="",Guid? warehouseId=null){if(method is not ("Cash" or "Card" or "MFS"))return BadRequest("Choose payment method.");if(method!="Cash"&&string.IsNullOrWhiteSpace(reference))return BadRequest("Verified terminal reference required.");var wh=await db.Set<Warehouse>().Where(x=>x.Active&&(!warehouseId.HasValue||x.Id==warehouseId)).OrderBy(x=>x.CreatedAt).FirstAsync();var zone=await db.Set<DeliveryZone>().SingleOrDefaultAsync(x=>x.Name=="POS pickup");if(zone==null){zone=new DeliveryZone{Name="POS pickup",Charge=0};db.Add(zone);await db.SaveChangesAsync();}
  var o=await orders.CheckoutAsync(new CheckoutRequest(Uid,name,phone,User.Identity?.Name??"","Store pickup",zone.Id,wh.Id,coupon,"COD",key,"POS","pos:"+Uid));
  if(method!="Cash")await orders.CollectTerminalAsync(o.Id,method,reference,Uid);
  foreach(var next in new[]{OrderStatus.Confirmed,OrderStatus.Processing,OrderStatus.Packed,OrderStatus.ReadyToShip,OrderStatus.Shipped,OrderStatus.OutForDelivery,OrderStatus.Delivered}){if((int)o.Status<(int)next)await orders.TransitionAsync(o.Id,next,Uid);}
  if(method=="Cash")await orders.CollectCodAsync(o.Id,Uid);return Redirect("/orders/"+o.Id);
 }
 [HttpPost("scan")]public async Task<IActionResult> Scan(string barcode){var sku=await db.Set<ProductSku>().SingleOrDefaultAsync(x=>x.Active&&(x.SKU==barcode||x.Barcode==barcode));if(sku==null)return BadRequest("SKU/barcode not found.");await carts.SetAsync("pos:"+Uid,sku.Id,1,true);return RedirectToAction(nameof(Index));}

}
