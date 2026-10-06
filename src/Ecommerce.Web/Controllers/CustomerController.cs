using System.Security.Claims;
using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
[Authorize,Route("customer")]
public class CustomerController(AppDb db):Controller {
 string Uid=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 [HttpGet("wishlist")]public async Task<IActionResult> Wishlist()=>View(await db.Set<WishlistItem>().Include(x=>x.Product).ThenInclude(x=>x.Skus).Where(x=>x.UserId==Uid).ToListAsync());
 [HttpPost("wishlist")]public async Task<IActionResult> Wishlist(Guid productId){var p=await db.Set<Product>().FindAsync(productId);if(p==null)return NotFound();var existing=await db.Set<WishlistItem>().SingleOrDefaultAsync(x=>x.UserId==Uid&&x.ProductId==productId);if(existing==null)db.Add(new WishlistItem{UserId=Uid,ProductId=productId});else db.Remove(existing);await db.SaveChangesAsync();return RedirectToAction(nameof(Wishlist));}
 [HttpPost("wishlist-cart")]public async Task<IActionResult> WishlistCart(Guid productId,Guid skuId){var wish=await db.Set<WishlistItem>().SingleOrDefaultAsync(x=>x.UserId==Uid&&x.ProductId==productId);if(wish==null||!await db.Set<ProductSku>().AnyAsync(x=>x.Id==skuId&&x.ProductId==productId&&x.Active))return NotFound();await new CartService(db).SetAsync(Uid,skuId,1,true);db.Remove(wish);await db.SaveChangesAsync();return Redirect("/cart");}
 [HttpPost("review")]public async Task<IActionResult> Review(Guid productId,int rating,string comment){if(rating<1||rating>5||string.IsNullOrWhiteSpace(comment)||comment.Length>2000)return BadRequest("Invalid review.");var p=await db.Set<Product>().FindAsync(productId);if(p==null)return NotFound();db.Add(new Review{ProductId=productId,UserId=Uid,Rating=rating,Comment=comment,VerifiedPurchase=await db.Set<OrderLine>().AnyAsync(x=>x.Order.UserId==Uid&&x.Order.Status==OrderStatus.Delivered&&x.Sku.ProductId==productId)});await db.SaveChangesAsync();TempData["Message"]="Review is awaiting moderation.";return Redirect("/product/"+p.Slug);}
 [HttpGet("support")]public async Task<IActionResult> Support()=>View(await db.Set<SupportTicket>().Where(x=>x.UserId==Uid).OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync());
 [HttpPost("support")]public async Task<IActionResult> Support(string subject,string message){if(string.IsNullOrWhiteSpace(subject)||string.IsNullOrWhiteSpace(message)||message.Length>2000)return BadRequest("Enter subject and message (max 2000 characters).");db.Add(new SupportTicket{UserId=Uid,Subject=subject,Message=message});await db.SaveChangesAsync();return RedirectToAction(nameof(Support));}
 [HttpGet("ticket/{id:guid}")]public async Task<IActionResult> Ticket(Guid id){var ticket=await db.Set<SupportTicket>().SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==Uid);if(ticket==null)return NotFound();ViewBag.Replies=await db.Set<SupportReply>().Where(x=>x.SupportTicketId==id).OrderBy(x=>x.CreatedAt).ToListAsync();return View(ticket);}
 [HttpPost("reply")]public async Task<IActionResult> Reply(Guid id,string message){if(string.IsNullOrWhiteSpace(message)||message.Length>2000)return BadRequest();var t=await db.Set<SupportTicket>().SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==Uid);if(t==null)return NotFound();if(t.Status=="Closed")return BadRequest("Ticket is closed.");db.Add(new SupportReply{SupportTicketId=id,AuthorId=Uid,Message=message});await db.SaveChangesAsync();return RedirectToAction(nameof(Ticket),new{id});}

}
