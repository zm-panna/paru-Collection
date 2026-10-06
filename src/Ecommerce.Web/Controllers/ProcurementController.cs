using System.Security.Claims;
using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
[Route("admin/procurement")]
public class ProcurementController(AppDb db,ProcurementService service):Controller{
 string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 [HttpGet(""),Permit("purchase.view")]public async Task<IActionResult> Index(){ViewBag.Skus=await db.Set<ProductSku>().Where(x=>x.Active).ToListAsync();ViewBag.Warehouses=await db.Set<Warehouse>().Where(x=>x.Active).ToListAsync();ViewBag.Suppliers=await db.Set<Supplier>().Where(x=>x.Active).ToListAsync();ViewBag.Quotes=await db.Set<SupplierQuotation>().Include(x=>x.Supplier).Include(x=>x.Lines).OrderBy(x=>x.Total).ToListAsync();ViewBag.Purchases=await db.Set<PurchaseOrder>().Include(x=>x.Lines).ThenInclude(x=>x.Sku).OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync();return View(await db.Set<PurchaseRequisition>().Include(x=>x.Lines).ThenInclude(x=>x.Sku).OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync());}
 [HttpPost("request"),Permit("purchase.create")]public async Task<IActionResult> CreateRequest(Guid warehouse,string purpose,Guid[] sku,int[] quantity){await service.RequestAsync(warehouse,purpose,sku,quantity,Actor);return RedirectToAction(nameof(Index));}
 [HttpPost("approve"),Permit("purchase.approve")]public async Task<IActionResult> Approve(Guid id){await service.ApproveAsync(id,Actor);return RedirectToAction(nameof(Index));}
 [HttpPost("quote"),Permit("purchase.create")]public async Task<IActionResult> Quote(Guid id,Guid supplier,DateTime until,string reference,Guid[] lineId,decimal[] cost){await service.QuoteAsync(id,supplier,until,reference,lineId,cost,Actor);return RedirectToAction(nameof(Index));}
 [HttpPost("award"),Permit("purchase.approve")]public async Task<IActionResult> Award(Guid id){await service.AwardAsync(id,Actor);return Redirect("/admin/operations/purchases");}
 [HttpPost("receive"),Permit("purchase.edit")]public async Task<IActionResult> Receive(Guid id,Guid[] lineId,int[] quantity,string reference){await service.ReceiveAsync(id,lineId,quantity,reference,Actor);return RedirectToAction(nameof(Index));}
 [HttpPost("return"),Permit("purchase.edit")]public async Task<IActionResult> Return(Guid lineId,int quantity,string reference,string reason){await service.ReturnAsync(lineId,quantity,reference,reason,Actor);return RedirectToAction(nameof(Index));}
}
