using System.Security.Claims;
using Ecommerce.Domain;
using Ecommerce.Application;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

[AllowAnonymous]
[Route("checkout")]
[EnableRateLimiting("account")]
public class CheckoutController : Controller
{
    private readonly AppDb db;
    private readonly OrderService orders;
    private readonly IConfiguration config;
    private readonly UserManager<IdentityUser> users;
    private readonly SignInManager<IdentityUser> signIn;

    public CheckoutController(
        AppDb db,
        OrderService orders,
        IConfiguration config,
        UserManager<IdentityUser> users,
        SignInManager<IdentityUser> signIn)
    {
        this.db = db;
        this.orders = orders;
        this.config = config;
        this.users = users;
        this.signIn = signIn;
    }

    // =========================================================
    // CURRENT / GUEST CUSTOMER
    // =========================================================
    private async Task<string> Customer()
    {
        // Logged-in customer
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.FindFirstValue(
                ClaimTypes.NameIdentifier)!;
        }

        // Check whether guest checkout is enabled
        var guestCheckoutEnabled =
            await new BusinessRulesService(db)
                .SettingAsync(
                    "GuestCheckoutEnabled",
                    "true");

        if (!string.Equals(
                guestCheckoutEnabled,
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(
                "Sign in before checkout.");
        }

        // Create temporary guest customer
        var id = Guid.NewGuid().ToString("N");

        var guest = new IdentityUser
        {
            UserName = "guest-" + id,
            Email = id + "@guest.invalid"
        };

        var result = await users.CreateAsync(guest);

        if (!result.Succeeded)
        {
            throw new BusinessException(
                "Guest checkout could not be started.");
        }

        await users.AddClaimAsync(
            guest,
            new Claim(
                "GuestCheckout",
                "true"));

        await signIn.SignInAsync(
            guest,
            isPersistent: false);

        HttpContext.User =
            await signIn.CreateUserPrincipalAsync(guest);

        // Merge guest cart with newly created customer
        if (Guid.TryParse(
                Request.Cookies["guest_cart"],
                out var cart))
        {
            await new CartService(db)
                .MergeAsync(
                    "g:" + cart,
                    guest.Id);
        }

        return guest.Id;
    }

    // =========================================================
    // CHECKOUT LISTS
    // =========================================================
    private async Task Lists()
    {
        ViewBag.Zones = await db
            .Set<DeliveryZone>()
            .AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.Name)
            .ToListAsync();

        ViewBag.SSL =
            config.GetValue<bool>(
                "Payments:SSLCommerz:Enabled");
    }


    // =========================================================
    // GET: /checkout
    // =========================================================
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        await Lists();

        Address? address = null;

        // Don't query Address with null UserId
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                address = await db
                    .Set<Address>()
                    .AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.IsDefault)
                    .ThenByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync();
            }
        }

        var model = new CheckoutVm
        {
            Email =
                User.HasClaim(
                    "GuestCheckout",
                    "true")
                    ? ""
                    : User.Identity?.Name ?? "",

            Name = address?.Recipient ?? "",

            Phone = address?.Phone ?? "",

            Address = address == null
                ? ""
                : BuildAddress(address)
        };

        return View(model);
    }

    // =========================================================
    // POST: /checkout/review
    // =========================================================
    [HttpPost("review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(
        CheckoutVm model)
    {
        await Lists();

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        try
        {
            var userId = await Customer();

            // -------------------------------------------------
            // CART
            // -------------------------------------------------
            var cart = await db
                .Set<CartItem>()
                .AsNoTracking()
                .Include(x => x.Sku)
                    .ThenInclude(x => x.Product)
                .Where(x => x.Owner == userId)
                .ToListAsync();

            if (cart.Count == 0)
            {
                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            // -------------------------------------------------
            // DELIVERY ZONE
            // -------------------------------------------------
            var zone = await db
                .Set<DeliveryZone>()
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.Id == model.ZoneId &&
                    x.Active);

            if (zone == null)
            {
                ModelState.AddModelError(
                    nameof(model.ZoneId),
                    "Invalid delivery zone.");

                return View("Index", model);
            }

            // -------------------------------------------------
            // CURRENT PRODUCT PRICES
            // -------------------------------------------------
            var prices =
                await new CampaignService(db)
                    .QuoteAsync(
                        cart,
                        userId);

            foreach (var item in cart)
            {
                if (prices.TryGetValue(
                        item.SkuId,
                        out var quoted))
                {
                    item.Sku.SalePrice =
                        quoted.Price;
                }
            }

            // -------------------------------------------------
            // SUBTOTAL
            // Quantity × Price
            // -------------------------------------------------
            var subtotal = cart.Sum(x =>
                x.Quantity *
                x.Sku.SalePrice);

            // -------------------------------------------------
            // COUPON / DISCOUNT
            // -------------------------------------------------
            decimal discount = 0;

            if (!string.IsNullOrWhiteSpace(
                    model.Coupon))
            {
                var couponCode =
                    model.Coupon.Trim();

                var coupon = await db
                    .Set<Coupon>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x =>
                        x.Code == couponCode);

                if (coupon == null)
                {
                    throw new BusinessException(
                        "Coupon not found.");
                }

                var totalUses = await db
                    .Set<CouponUse>()
                    .CountAsync(x =>
                        x.CouponId == coupon.Id);

                var customerUses = await db
                    .Set<CouponUse>()
                    .CountAsync(x =>
                        x.CouponId == coupon.Id &&
                        x.UserId == userId);

                discount =
                    CommerceRules.Discount(
                        coupon,
                        subtotal,
                        totalUses,
                        customerUses,
                        DateTime.UtcNow);
            }

            // -------------------------------------------------
            // VAT / TAX
            // -------------------------------------------------
            var tax = cart.Sum(item =>
            {
                var lineTotal =
                    item.Quantity *
                    item.Sku.SalePrice;

                var lineDiscount =
                    subtotal == 0
                        ? 0
                        : discount *
                          lineTotal /
                          subtotal;

                var taxableAmount =
                    lineTotal -
                    lineDiscount;

                return CommerceRules.Money(
                    taxableAmount *
                    item.Sku.VatPercent /
                    100);
            });

            // -------------------------------------------------
            // SHIPPING
            // -------------------------------------------------
            decimal shipping;

            if (zone.FreeAbove.HasValue &&
                subtotal - discount >=
                zone.FreeAbove.Value)
            {
                shipping = 0;
            }
            else
            {
                shipping = zone.Charge;
            }

            // -------------------------------------------------
            // GRAND TOTAL
            // -------------------------------------------------
            var total =
                subtotal -
                discount +
                tax +
                shipping;

            ViewBag.Cart = cart;

            ViewBag.Subtotal =
                CommerceRules.Money(subtotal);

            ViewBag.Discount =
                CommerceRules.Money(discount);

            ViewBag.Tax =
                CommerceRules.Money(tax);

            ViewBag.Shipping =
                CommerceRules.Money(shipping);

            ViewBag.Total =
                CommerceRules.Money(total);

            return View(model);
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(
                "",
                ex.Message);

            return View("Index", model);
        }
    }

    // =========================================================
    // POST: /checkout
    // PLACE ORDER
    // =========================================================
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        CheckoutVm model)
    {
        await Lists();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // -----------------------------------------------------
        // PAYMENT METHOD VALIDATION
        // -----------------------------------------------------
        if (string.Equals(
                model.Method,
                "SSLCommerz",
                StringComparison.OrdinalIgnoreCase)
            &&
            !config.GetValue<bool>(
                "Payments:SSLCommerz:Enabled"))
        {
            ModelState.AddModelError(
                nameof(model.Method),
                "SSLCommerz is disabled.");

            return View(model);
        }

        try
        {
            // -------------------------------------------------
            // CUSTOMER
            // -------------------------------------------------
            var customerId =
                await Customer();

            // -------------------------------------------------
            // DEFAULT / SELECTED WAREHOUSE
            // -------------------------------------------------
            var warehouseId =
                await new BusinessRulesService(db)
                    .WarehouseAsync();

            // -------------------------------------------------
            // CHECKOUT
            // -------------------------------------------------
            var request =
                new CheckoutRequest(
                    customerId,
                    model.Name,
                    model.Phone,
                    model.Email,
                    model.Address,
                    model.ZoneId,
                    warehouseId,
                    model.Coupon,
                    model.Method,
                    model.Key);

            var order =
                await orders.CheckoutAsync(
                    request);

            // -------------------------------------------------
            // ORDER DETAILS
            // -------------------------------------------------
            return Redirect(
                "/orders/" + order.Id);
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(
                "",
                ex.Message);

            return View(model);
        }
    }

    // =========================================================
    // ADDRESS FORMAT
    // =========================================================
    private static string BuildAddress(
        Address address)
    {
        var parts = new[]
        {
            address.Street,
            address.Area,
            address.District
        };

        return string.Join(
            ", ",
            parts.Where(x =>
                !string.IsNullOrWhiteSpace(x)));
    }
}