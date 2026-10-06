using System.Security.Claims;
using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
[Route("admin"),Permit("dashboard.view")]
public class AdminController(AppDb db,AccessService access):Controller {


 [HttpGet("")]public async Task<IActionResult> Index(){var model=new Dictionary<string,string>();if(await access.HasAsync(User,"orders.view")){var today=DateTime.UtcNow.Date;model["Orders today"]=(await db.Set<Order>().CountAsync(x=>x.CreatedAt>=today)).ToString();model["Pending orders"]=(await db.Set<Order>().CountAsync(x=>x.Status==OrderStatus.Pending)).ToString();model["Delivered orders"]=(await db.Set<Order>().CountAsync(x=>(x.Status==OrderStatus.Delivered||x.Status==OrderStatus.Returned||x.Status==OrderStatus.Refunded))).ToString();}if(await access.HasAsync(User,"reports.view")){model["Gross orders (BDT)"]=(await db.Set<Order>().Where(x=>x.Status!=OrderStatus.Cancelled).Select(x=>x.Total).ToListAsync()).Sum().ToString("N2");model["Collected (BDT)"]=(await db.Set<PaymentTransaction>().Where(x=>x.Status=="Paid").Select(x=>x.Amount).ToListAsync()).Sum().ToString("N2");}if(await access.HasAsync(User,"inventory.view"))model["Low stock SKUs"]=(await db.Set<StockBalance>().CountAsync(x=>x.OnHand-x.Reserved<=x.Sku.ReorderLevel)).ToString();var chartOrders=await access.HasAsync(User,"reports.view")?await db.Set<Order>().Where(x=>x.CreatedAt>=DateTime.UtcNow.AddDays(-30)).ToListAsync():new List<Order>();
 ViewBag.Daily=chartOrders.Where(x=>(x.Status==OrderStatus.Delivered||x.Status==OrderStatus.Returned||x.Status==OrderStatus.Refunded)).GroupBy(x=>x.CreatedAt.Date).OrderBy(x=>x.Key).Select(g=>(Label:g.Key.ToString("MM-dd"),Value:g.Sum(x=>x.Total))).ToList();
 if(await access.HasAsync(User,"reports.view")){
 var today=DateTime.UtcNow.Date;var collected=await db.Set<PaymentTransaction>().Where(x=>x.Status=="Paid").ToListAsync();
 model["Today's collection (BDT)"]=collected.Where(x=>x.VerifiedAt>=today).Sum(x=>x.Amount).ToString("N2");model["Online payments (BDT)"]=collected.Where(x=>x.Gateway=="SSLCommerz").Sum(x=>x.Amount).ToString("N2");
 var deliveredIds=await db.Set<OrderHistory>().Where(x=>x.Status=="Delivered"&&x.CreatedAt>=today).Select(x=>x.OrderId).Distinct().ToListAsync();model["Today's delivered sales (BDT)"]=(await db.Set<Order>().Where(x=>deliveredIds.Contains(x.Id)).Select(x=>x.Total).ToListAsync()).Sum().ToString("N2");
 model["Processing orders"]=(await db.Set<Order>().CountAsync(x=>x.Status==OrderStatus.Processing)).ToString();model["Cancelled orders"]=(await db.Set<Order>().CountAsync(x=>x.Status==OrderStatus.Cancelled)).ToString();model["Returns"]=(await db.Set<ReturnRequest>().CountAsync()).ToString();model["Refunds"]=(await db.Set<PaymentRefund>().CountAsync(x=>x.Status=="Completed")).ToString();model["Customers"]=(await db.UserRoles.CountAsync(x=>db.Roles.Any(r=>r.Id==x.RoleId&&r.Name=="Customer"))).ToString();
 var lines=await db.Set<OrderLine>().Include(x=>x.Sku).ThenInclude(x=>x.Product).ThenInclude(x=>x.Category).Where(x=>(x.Order.Status==OrderStatus.Delivered||x.Order.Status==OrderStatus.Returned||x.Order.Status==OrderStatus.Refunded)&&x.Order.CreatedAt>=DateTime.UtcNow.AddYears(-1)).ToListAsync();
 ViewBag.CategorySales=lines.GroupBy(x=>x.Sku.Product.Category.Name).Select(g=>(Label:g.Key,Value:g.Sum(x=>x.Price*x.Quantity))).OrderByDescending(x=>x.Value).Take(8).ToList();ViewBag.TopProducts=lines.GroupBy(x=>x.Sku.Product.Name).Select(g=>(Label:g.Key,Value:(decimal)g.Sum(x=>x.Quantity))).OrderByDescending(x=>x.Value).Take(8).ToList();
 ViewBag.PaymentMethods=collected.GroupBy(x=>x.Gateway).Select(g=>(Label:g.Key,Value:g.Sum(x=>x.Amount))).ToList();var annual=await db.Set<Order>().Where(x=>(x.Status==OrderStatus.Delivered||x.Status==OrderStatus.Returned||x.Status==OrderStatus.Refunded)&&x.CreatedAt>=DateTime.UtcNow.AddYears(-1)).ToListAsync();ViewBag.Monthly=annual.GroupBy(x=>x.CreatedAt.ToString("yyyy-MM")).OrderBy(x=>x.Key).Select(g=>(Label:g.Key,Value:g.Sum(x=>x.Total))).ToList();var firstOrders=await db.Set<Order>().GroupBy(x=>x.UserId).Select(g=>g.Min(x=>x.CreatedAt)).ToListAsync();ViewBag.CustomerGrowth=firstOrders.GroupBy(x=>x.ToString("yyyy-MM")).OrderBy(x=>x.Key).Select(g=>(Label:g.Key,Value:(decimal)g.Count())).ToList();
 }
 ViewBag.Statuses=chartOrders.GroupBy(x=>x.Status).Select(g=>(Label:g.Key.ToString(),Value:(decimal)g.Count())).ToList();return View(model);}
}
