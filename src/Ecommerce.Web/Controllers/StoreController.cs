using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
public class StoreController(AppDb db,IConfiguration config):Controller {

    [HttpGet("/")]
    [HttpGet("products")]
    public async Task<IActionResult> Index(
    string q = "",
    Guid? category = null,
    string sort = "new",
    decimal? min = null,
    decimal? max = null,
    string? color = null,
    string? size = null,
    bool available = false,
    Guid? brand = null,
    int? rating = null,
    int page = 1,
    bool discounted = false,
    string collection = "")
    {
        var now = DateTime.UtcNow;

  

        // ---------------------------------------------------------
        // 1. BANNERS
        // ---------------------------------------------------------
        ViewBag.Banners = await db.Set<Banner>()
            .AsNoTracking()
            .Where(x =>
                x.Active &&
                x.StartsAt <= now &&
                x.EndsAt > now)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        // ---------------------------------------------------------
        // 2. BRANDS
        // ---------------------------------------------------------
        ViewBag.Brands = await db.Set<Brand>()
          .AsNoTracking()
          .Where(x => x.Active)
          .OrderBy(x => x.Name)
          .ToListAsync();
        // ---------------------------------------------------------
        // 3. POPULAR SEARCHES
        // Important: Query may be nullable in old database data.
        // ---------------------------------------------------------
        ViewBag.Popular = await db.Set<SearchEvent>()
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt >= now.AddDays(-30) &&
                x.Query != null &&
                x.Query != "")
            .GroupBy(x => x.Query)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(8)
            .ToListAsync();

        // ---------------------------------------------------------
        // 4. CURRENT USER
        // ---------------------------------------------------------
        var uid = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier
        )?.Value;

        // ---------------------------------------------------------
        // 5. SEARCH HISTORY
        // ---------------------------------------------------------
        if (string.IsNullOrWhiteSpace(uid))
        {
            ViewBag.History = new List<string>();
        }
        else
        {
            ViewBag.History = await db.Set<SearchEvent>()
                .AsNoTracking()
                .Where(x =>
                    x.UserId == uid &&
                    x.Query != null &&
                    x.Query != "")
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.Query)
                .Take(8)
                .ToListAsync();
        }

        // ---------------------------------------------------------
        // 6. RECOMMENDATIONS
        // ---------------------------------------------------------
        try
        {
            ViewBag.Recommended =
                await new RecommendationService(db)
                    .ForAsync(uid, 4);
        }
        catch
        {
            // Recommendation should never break storefront.
            ViewBag.Recommended = new List<Product>();
        }

        // ---------------------------------------------------------
        // 7. SAVE SEARCH EVENT
        // ---------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();

            if (q.Length > 100)
            {
                q = q[..100];
            }

            var searchUserId = uid ?? string.Empty;

            var alreadySaved = await db.Set<SearchEvent>()
                .AnyAsync(x =>
                    x.UserId == searchUserId &&
                    x.Query == q &&
                    x.CreatedAt >= now.AddMinutes(-5));

            if (!alreadySaved)
            {
                db.Add(new SearchEvent
                {
                    UserId = searchUserId,
                    Query = q
                });

                await db.SaveChangesAsync();
            }
        }

        // ---------------------------------------------------------
        //// 8. CATEGORY SEO
        //// ---------------------------------------------------------
        //if (category.HasValue)
        //{
        //    var cat = await db.Set<Category>()
        //        .AsNoTracking()
        //        .FirstOrDefaultAsync(x => x.Id == category.Value);

        //    if (cat != null)
        //    {
        //        ViewData["Title"] =
        //            !string.IsNullOrWhiteSpace(cat.MetaTitle)
        //                ? cat.MetaTitle
        //                : cat.Name;

        //        ViewData["Description"] =
        //            cat.MetaDescription ?? string.Empty;
        //    }
        //}

        //SetCanonical(
        //    category.HasValue
        //        ? $"/products?category={category.Value}"
        //        : "/products"
        //);

        // ---------------------------------------------------------
        // 8. CATEGORY SEO
        // ---------------------------------------------------------
        if (category.HasValue)
        {
            var cat = await db.Set<Category>()
                .AsNoTracking()
                .Where(x => x.Id == category.Value)
                .Select(x => new
                {
                    x.Name,
                    x.MetaTitle,
                    x.MetaDescription
                })
                .FirstOrDefaultAsync();

            if (cat != null)
            {
                ViewData["Title"] =
                    !string.IsNullOrWhiteSpace(cat.MetaTitle)
                        ? cat.MetaTitle
                        : cat.Name;

                ViewData["Description"] =
                    cat.MetaDescription ?? string.Empty;
            }
        }

        SetCanonical(
            category.HasValue
                ? $"/products?category={category.Value}"
                : "/products"
        );

        // ---------------------------------------------------------
        // 9. PAGE VALIDATION
        // ---------------------------------------------------------
        page = Math.Clamp(page, 1, 10000);

        // ---------------------------------------------------------
        // 10. BASE PRODUCT QUERY
        // ---------------------------------------------------------
        IQueryable<Product> query = db.Set<Product>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Skus)
            .Include(x => x.Files)
            .Where(x => x.Active);


        // ---------------------------------------------------------
        // 11. SEARCH
        // ---------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim();

            query = query.Where(x =>

                x.Name.Contains(search)

                ||

                x.Skus.Any(s =>
                    (s.SKU != null &&
                     s.SKU.Contains(search))

                    ||

                    (s.Barcode != null &&
                     s.Barcode.Contains(search))
                )

                ||

                (
                    x.Category != null &&
                    x.Category.Name != null &&
                    x.Category.Name.Contains(search)
                )

                ||

                (
                    x.Brand != null &&
                    x.Brand.Name != null &&
                    x.Brand.Name.Contains(search)
                )
            );
        }

        // ---------------------------------------------------------
        // 12. CATEGORY FILTER
        // ---------------------------------------------------------
        if (category.HasValue)
        {
            query = query.Where(
                x => x.CategoryId == category.Value
            );
        }

        // ---------------------------------------------------------
        // 13. PRICE FILTER
        // ---------------------------------------------------------
        if (min.HasValue || max.HasValue)
        {
            query = query.Where(x =>
                x.Skus.Any(s =>
                    s.Active &&
                    (!min.HasValue ||
                     s.SalePrice >= min.Value) &&
                    (!max.HasValue ||
                     s.SalePrice <= max.Value)
                )
            );
        }

        // ---------------------------------------------------------
        // 14. COLOR FILTER
        // ---------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(color))
        {
            query = query.Where(x =>
                x.Skus.Any(s =>
                    s.Active &&
                    s.Color != null &&
                    s.Color == color)
            );
        }

        // ---------------------------------------------------------
        // 15. SIZE FILTER
        // ---------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(size))
        {
            query = query.Where(x =>
                x.Skus.Any(s =>
                    s.Active &&
                    s.Size != null &&
                    s.Size == size)
            );
        }

        // ---------------------------------------------------------
        // 16. COLLECTION
        // ---------------------------------------------------------
        switch ((collection ?? string.Empty).ToLowerInvariant())
        {
            case "featured":
                query = query.Where(x => x.Featured);
                break;

            case "new":
                query = query.Where(x => x.NewArrival);
                break;

            case "best":
                query = query.Where(x => x.BestSeller);
                break;
        }

        // ---------------------------------------------------------
        // 17. DISCOUNT
        // ---------------------------------------------------------
        if (discounted)
        {
            query = query.Where(x =>

                x.Skus.Any(s =>
                    s.Active &&
                    s.SalePrice < s.RegularPrice)

                ||

                db.Set<Campaign>().Any(c =>
                    c.Active &&
                    c.CustomerId == "" &&
                    c.StartsAt <= now &&
                    c.EndsAt > now &&
                    (!c.ProductId.HasValue ||
                     c.ProductId == x.Id) &&
                    (!c.CategoryId.HasValue ||
                     c.CategoryId == x.CategoryId))
            );
        }

        // ---------------------------------------------------------
        // 18. BRAND
        // ---------------------------------------------------------
        if (brand.HasValue)
        {
            query = query.Where(
                x => x.BrandId == brand.Value
            );
        }

        // ---------------------------------------------------------
        // 19. STOCK AVAILABILITY
        // ---------------------------------------------------------
        if (available)
        {
            query = query.Where(x =>
                x.Skus.Any(s =>
                    s.Active &&
                    db.Set<StockBalance>().Any(b =>
                        b.SkuId == s.Id &&
                        b.OnHand > b.Reserved))
            );
        }

        // ---------------------------------------------------------
        // 20. RATING
        // ---------------------------------------------------------
        if (rating.HasValue)
        {
            var requestedRating = rating.Value;

            query = query.Where(x =>
                db.Set<Review>()
                    .Where(r =>
                        r.ProductId == x.Id &&
                        r.Approved)
                    .Average(r => (double?)r.Rating)
                >= requestedRating
            );
        }

        // ---------------------------------------------------------
        // 21. SORT
        // ---------------------------------------------------------
        query = sort switch
        {
            "price" =>
                query.OrderBy(x =>
                    x.Skus
                        .Where(s => s.Active)
                        .Min(s => (decimal?)s.SalePrice)
                        ?? decimal.MaxValue),

            "price-desc" =>
                query.OrderByDescending(x =>
                    x.Skus
                        .Where(s => s.Active)
                        .Max(s => (decimal?)s.SalePrice)
                        ?? 0),

            "name" =>
                query.OrderBy(x => x.Name),

            "popular" =>
                query.OrderByDescending(x =>
                    db.Set<OrderLine>()
                        .Where(l =>
                            l.Sku.ProductId == x.Id &&
                            l.Order.Status ==
                                OrderStatus.Delivered)
                        .Sum(l => (int?)l.Quantity)
                    ?? 0),

            "rating" =>
                query.OrderByDescending(x =>
                    db.Set<Review>()
                        .Where(r =>
                            r.ProductId == x.Id &&
                            r.Approved)
                        .Average(r => (double?)r.Rating)
                    ?? 0),

            _ =>
                query.OrderByDescending(x => x.CreatedAt)
        };

        // ---------------------------------------------------------
        // 22. HOME COLLECTIONS
        // ---------------------------------------------------------
        if (Request.Path == "/")
        {
            IQueryable<Product> home = db.Set<Product>()
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.Skus)
                .Include(x => x.Files)
                .Where(x =>
                    x.Active &&
                    x.Skus.Any(s => s.Active));

            var featured = await home
                .Where(x => x.Featured)
                .OrderByDescending(x => x.CreatedAt)
                .Take(4)
                .ToListAsync();

            var newArrivals = await home
                .Where(x => x.NewArrival)
                .OrderByDescending(x => x.CreatedAt)
                .Take(4)
                .ToListAsync();

            var bestSellers = await home
                .OrderByDescending(x =>
                    db.Set<OrderLine>()
                        .Where(l =>
                            l.Sku.ProductId == x.Id &&
                            (
                                l.Order.Status ==
                                    OrderStatus.Delivered

                                ||

                                l.Order.Status ==
                                    OrderStatus.Returned

                                ||

                                l.Order.Status ==
                                    OrderStatus.Refunded
                            ))
                        .Sum(l => (int?)l.Quantity)
                    ?? 0)
                .Take(4)
                .ToListAsync();

            var flashOffers = await home
                .Where(x =>
                    db.Set<Campaign>().Any(c =>
                        c.Active &&
                        c.CustomerId == "" &&
                        c.StartsAt <= now &&
                        c.EndsAt > now &&
                        (!c.ProductId.HasValue ||
                         c.ProductId == x.Id) &&
                        (!c.CategoryId.HasValue ||
                         c.CategoryId == x.CategoryId)))
                .Take(4)
                .ToListAsync();

            ViewBag.HomeCollections =
                new Dictionary<string, List<Product>>
                {
                    ["Featured"] = featured,
                    ["New arrivals"] = newArrivals,
                    ["Best sellers"] = bestSellers,
                    ["Flash offers"] = flashOffers
                };
        }

        // ---------------------------------------------------------
        // 23. PAGINATION
        // Take 13 because 13th record tells us Next page exists.
        // ---------------------------------------------------------
        var products = await query
            .Skip((page - 1) * 12)
            .Take(13)
            .ToListAsync();

        var hasNextPage = products.Count > 12;

        var pageProducts = products
            .Take(12)
            .ToList();

        // ---------------------------------------------------------
        // 24. CATEGORY MENU
        // ---------------------------------------------------------


        var categories = await db.Set<Category>()
        .AsNoTracking()
        .Where(x => x.Active)
        .OrderBy(x => x.SortOrder)
        .Select(x => new CategoryVm
        {
            Id = x.Id,
            Name = x.Name ?? string.Empty,
            Slug = x.Slug ?? string.Empty,

            ImagePath = x.ImagePath ?? string.Empty,

            SortOrder = x.SortOrder,
            Active = x.Active
        })
        .ToListAsync();

        // ---------------------------------------------------------
        // 25. RETURN
        // ---------------------------------------------------------
        var vm = new CatalogVm(
            pageProducts,
            categories,
            q,
            category,
            sort,
            page,
            hasNextPage
        );

        return View(vm);
    }

    // ============================================================
    // PRODUCT DETAILS
    // ============================================================

    [HttpGet("product/{slug}")]
    public async Task<IActionResult> Product(string slug)
    {
        // --------------------------------------------------------
        // 1. Validate slug
        // --------------------------------------------------------
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        slug = slug.Trim();

        // --------------------------------------------------------
        // 2. Load product
        // AsSplitQuery prevents Skus x Files cartesian explosion.
        // --------------------------------------------------------
        var product = await db.Set<Product>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Skus)
            .Include(x => x.Files)
            .Include(x => x.Category)
            .SingleOrDefaultAsync(x =>
                x.Active &&
                x.Slug == slug);

        if (product == null)
        {
            return NotFound();
        }

        // --------------------------------------------------------
        // 3. SEO
        // --------------------------------------------------------
        ViewData["Title"] =
            !string.IsNullOrWhiteSpace(product.MetaTitle)
                ? product.MetaTitle
                : product.Name;

        ViewData["Description"] =
            product.MetaDescription ?? string.Empty;

        ViewData["Keywords"] =
            product.MetaKeywords ?? string.Empty;

        // --------------------------------------------------------
        // 4. Canonical URL
        // --------------------------------------------------------
        var canonicalSlug =
            !string.IsNullOrWhiteSpace(product.Slug)
                ? product.Slug
                : product.Id.ToString();

        SetCanonical(
            "/product/" +
            Uri.EscapeDataString(canonicalSlug)
        );

        // --------------------------------------------------------
        // 5. Current logged-in user
        // --------------------------------------------------------
        var uid = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier
        )?.Value;

        // --------------------------------------------------------
        // 6. Product view history
        // Do not create anonymous ProductView records.
        // --------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(uid))
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);

            var alreadyViewed = await db.Set<ProductView>()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.UserId == uid &&
                    x.ProductId == product.Id &&
                    x.CreatedAt >= oneHourAgo);

            if (!alreadyViewed)
            {
                db.Set<ProductView>().Add(
                    new ProductView
                    {
                        UserId = uid,
                        ProductId = product.Id
                    });

                await db.SaveChangesAsync();
            }
        }

        // --------------------------------------------------------
        // 7. Stock
        // Important:
        // Calculate only for current product.
        // --------------------------------------------------------
        ViewBag.Stock = await db.Set<StockBalance>()
            .AsNoTracking()
            .Where(x =>
                x.Sku != null &&
                x.Sku.ProductId == product.Id)
            .GroupBy(x => x.SkuId)
            .ToDictionaryAsync(
                x => x.Key,
                x => x.Sum(s =>
                    s.OnHand - s.Reserved)
            );

        // --------------------------------------------------------
        // 8. Reviews
        // --------------------------------------------------------
        ViewBag.Reviews = await db.Set<Review>()
            .AsNoTracking()
            .Where(x =>
                x.ProductId == product.Id &&
                x.Approved)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        // --------------------------------------------------------
        // 9. Related products
        // Include Files only if the View actually needs images.
        // --------------------------------------------------------
        ViewBag.Related = await db.Set<Product>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Files)
            .Include(x => x.Skus)
            .Where(x =>
                x.Active &&
                x.CategoryId == product.CategoryId &&
                x.Id != product.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(4)
            .ToListAsync();

        return View(product);
    }


    // ============================================================
    // SEARCH SUGGESTION
    // ============================================================

    [HttpGet("search/suggest")]
    public async Task<IActionResult> Suggest(string q = "")
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(Array.Empty<object>());
        }

        q = q.Trim();

        if (q.Length < 2)
        {
            return Json(Array.Empty<object>());
        }

        // Protect against unnecessarily large search input.
        if (q.Length > 100)
        {
            q = q[..100];
        }

        var result = await db.Set<Product>()
            .AsNoTracking()
            .Where(x =>
                x.Active &&
                x.Name != null &&
                x.Name.Contains(q))
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                Name = x.Name,
                Slug = x.Slug ?? string.Empty
            })
            .Take(8)
            .ToListAsync();

        return Json(result);
    }


    // ============================================================
    // CANONICAL URL
    // ============================================================

    private void SetCanonical(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var publicBaseUrl = config["PublicBaseUrl"];

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return;
        }

        if (!Uri.TryCreate(
                publicBaseUrl,
                UriKind.Absolute,
                out var origin))
        {
            return;
        }

        if (origin.Scheme != Uri.UriSchemeHttp &&
            origin.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }

        ViewData["Canonical"] =
            origin.GetLeftPart(UriPartial.Authority) +
            path;
    }


    // ============================================================
    // ABOUT
    // ============================================================

    [HttpGet("about")]
    public IActionResult About()
    {
        return View();
    }


    // ============================================================
    // OFFERS
    // ============================================================

    [HttpGet("offers")]
    public async Task<IActionResult> Offers()
    {
        var now = DateTime.UtcNow;

        var offers = await db.Set<Campaign>()
            .AsNoTracking()
            .Where(x =>
                x.Active &&
                x.CustomerId == "" &&
                x.StartsAt <= now &&
                x.EndsAt > now)
            .OrderBy(x => x.EndsAt)
            .ToListAsync();

        return View(offers);
    }


    // ============================================================
    // CONTENT PAGE
    // ============================================================

    [HttpGet("pages/{slug}")]
    public async Task<IActionResult> Page(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        slug = slug.Trim();

        var page = await db.Set<ContentPage>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.Active &&
                x.Slug == slug);

        if (page == null)
        {
            return NotFound();
        }

        return View(page);
    }


    // ============================================================
    // SEARCH LOG
    // ============================================================

    [HttpPost("search/log")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogSearch(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return NoContent();
        }

        q = q.Trim();

        if (q.Length > 100)
        {
            q = q[..100];
        }

        var uid = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier
        )?.Value ?? string.Empty;

        // Avoid saving the same search repeatedly within 5 minutes.
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

        var exists = await db.Set<SearchEvent>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == uid &&
                x.Query == q &&
                x.CreatedAt >= fiveMinutesAgo);

        if (!exists)
        {
            db.Set<SearchEvent>().Add(
                new SearchEvent
                {
                    Query = q,
                    UserId = uid
                });

            await db.SaveChangesAsync();
        }

        return NoContent();
    }

}
