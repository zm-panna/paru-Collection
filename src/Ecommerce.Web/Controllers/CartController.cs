using System.Security.Claims;
using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("cart")]
public class CartController : Controller
{
    private readonly AppDb db;
    private readonly CartService carts;

    public CartController(AppDb db, CartService carts)
    {
        this.db = db;
        this.carts = carts;
    }

    private string Owner()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        }

        var id = Request.Cookies["guest_cart"];

        if (!Guid.TryParse(id, out _))
        {
            id = Guid.NewGuid().ToString();

            Response.Cookies.Append(
                "guest_cart",
                id,
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    MaxAge = TimeSpan.FromDays(30)
                });
        }

        return "g:" + id;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var owner = Owner();

        var items = await db.Set<CartItem>()
            .AsNoTracking()
            .Include(x => x.Sku)
                .ThenInclude(x => x.Product)
            .Where(x => x.Owner == owner)
            .ToListAsync();

        return View(items);
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        Guid skuId,
        int quantity = 1)
    {
        if (quantity < 1)
            quantity = 1;

        await carts.SetAsync(
            Owner(),
            skuId,
            quantity,
            true);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid skuId, int quantity)
    {
        if (quantity < 1)
            quantity = 1;

        await carts.SetAsync(
            Owner(),
            skuId,
            quantity,
            false
        );

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("clear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        var owner = Owner();

        var items = await db.Set<CartItem>()
            .Where(x => x.Owner == owner)
            .ToListAsync();

        db.RemoveRange(items);

        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
