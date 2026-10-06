using System.Security.Claims;
using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
[Authorize,Route("orders")]
public class OrdersController(AppDb db,ReturnService returns,AccessService access):Controller {
 string Uid=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 [HttpGet("")]public async Task<IActionResult> Index()=>View(await db.Set<Order>().Where(x=>x.UserId==Uid).OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync());
 [HttpGet("{id:guid}")]public async Task<IActionResult> Details(Guid id){var staff=await access.HasAsync(User,"orders.view");var o=await db.Set<Order>().Include(x=>x.Lines).Include(x=>x.History).SingleOrDefaultAsync(x=>x.Id==id&&(x.UserId==Uid||staff));if(o==null)return NotFound();ViewBag.CanReturn=o.UserId==Uid;ViewBag.Allocations=await db.Set<OrderAllocation>().Include(x=>x.Warehouse).Include(x=>x.OrderLine).Where(x=>x.OrderLine.OrderId==id).ToListAsync();ViewBag.Deliveries=await db.Set<DeliveryHistory>().Where(x=>x.Delivery.OrderId==id).OrderBy(x=>x.CreatedAt).ToListAsync();ViewBag.Returns=await db.Set<ReturnRequest>().Where(x=>x.OrderId==id).ToListAsync();ViewBag.Refunds=await db.Set<PaymentRefund>().Where(x=>x.OrderId==id).ToListAsync();return View(o);}
 [HttpPost("return")]public async Task<IActionResult> Return(Guid lineId,int quantity,string reason){await returns.RequestAsync(lineId,quantity,reason,Uid);TempData["Message"]="Return request submitted.";return RedirectToAction(nameof(Index));}
}
