using System.Net;
using System.Text.RegularExpressions;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Ecommerce.IntegrationTests;

public class WebFixture : IAsyncLifetime
{
    string? adminCookie;

    public WebApplicationFactory<Program> Factory = null!;

    public HttpClient Client = null!;

    readonly SqliteConnection connection = new("Data Source=:memory:");

    public async Task InitializeAsync()
    {
        var sql = Environment.GetEnvironmentVariable("ERP_SQL_TEST_CONNECTION");
        if (sql != null && !new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql).InitialCatalog.StartsWith("Paru_Acceptance_")) throw new InvalidOperationException("Use a dedicated Paru_Acceptance_ database.");
        if (sql == null) await connection.OpenAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminEmail"] = "test-admin@example.com",
                ["Seed:AdminPassword"] = "OnlyForTests!12345"
            }));
            b.ConfigureServices(s =>
            {
                s.RemoveAll<AppDb>();
                if (sql == null) s.AddScoped<AppDb>(_ => new TestDb(new DbContextOptionsBuilder<AppDb>().UseSqlite(connection).Options));
                else s.AddScoped<AppDb>(_ => new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer(sql).Options));
            });
        });
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        if (sql == null) await db.Database.EnsureCreatedAsync();
        else await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<SeedData>().RunAsync();
        if (Environment.GetEnvironmentVariable("ERP_UI_CAPTURE") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "index.html"), await Client.GetStringAsync("/"));
            await File.WriteAllTextAsync(Path.Combine(output, "login.html"), await Client.GetStringAsync("/account/login"));
            using var admin = await AdminClient();
            await File.WriteAllTextAsync(Path.Combine(output, "admin.html"), await admin.GetStringAsync("/admin/data/products"));
        }
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await connection.DisposeAsync();
    }

    public static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Antiforgery field missing");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public async Task<HttpClient> AdminClient()
    {
        var c = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        if (adminCookie != null)
        {
            c.DefaultRequestHeaders.Add("Cookie", adminCookie);
            return c;
        }
        var page = await c.GetStringAsync("/account/login?admin=true");
        var response = await c.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", "test-admin@example.com" },
            { "Password", "OnlyForTests!12345" },
            { "Admin", "true" },
            { "__RequestVerificationToken", Token(page) }
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        adminCookie = string.Join("; ", response.Headers.GetValues("Set-Cookie").Where(x => x.StartsWith(".AspNetCore.Identity.Application")).Select(x => x.Split(';')[0]));
        return c;
    }
}

public class HttpTests(WebFixture fixture) : IClassFixture<WebFixture>
{

    [Theory]
    [InlineData("/")]
    [InlineData("/products")]
    [InlineData("/product/ladies-1")]
    [InlineData("/account/login")]
    [InlineData("/account/register")]
    [InlineData("/cart")]
    [InlineData("/about")]
    [InlineData("/offers")]

    public async Task PublicPagesRender(string url)
    {
        var r = await fixture.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("PARU", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]

    public async Task AdminRedirectsAnonymousToLogin()
    {
        var r = await fixture.Client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/account/login", r.Headers.Location!.ToString());
    }

    [Fact]

    public async Task MutatingPostRequiresAntiforgery()
    {
        var r = await fixture.Client.PostAsync("/cart/add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "skuId", Guid.NewGuid().ToString() },
            { "quantity", "1" }
        }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]

    public async Task AdminLoginAndMasterPagesRender()
    {
        using var client = await fixture.AdminClient();
        foreach (var path in new[]
        { "/admin/data/products", "/admin/data/products/edit", "/admin/security", "/admin/reports", "/admin/operations/inventory", "/admin/operations/purchases", "/admin/operations/orders", "/admin/operations/returns", "/admin/operations/accounts", "/admin/operations/support", "/admin/procurement", "/admin/finance", "/admin/data/campaigns", "/admin/data/periods" })
        {
            var r = await client.GetAsync(path);
            Assert.True(r.StatusCode == HttpStatusCode.OK, path + " returned " + r.StatusCode + ": " + await r.Content.ReadAsStringAsync());
        }
    }

    [Fact]

    public async Task ExcelExportIsAnActualZipWorkbook()
    {
        using var c = await fixture.AdminClient();
        var r = await c.GetAsync("/admin/reports/products?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]

    public async Task RegistrationCannotGrantAdminRole()
    {
        using var c = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var page = await c.GetStringAsync("/account/register");
        var r = await c.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", Guid.NewGuid() + "@example.com" },
            { "Phone", "01000000000" },
            { "Password", "CustomerTest!1234" },
            { "Role", "SuperAdmin" },
            { "__RequestVerificationToken", WebFixture.Token(page) }
        }));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var denied = await c.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/account/denied", denied.Headers.Location!.ToString());
    }

    [Fact]

    public async Task NewReportsAndMenuEditorsRender()
    {
        using var c = await fixture.AdminClient();
        foreach (var path in new[]
        { "/admin", "/admin/reports/kpi", "/admin/reports/sales", "/admin/reports/vat-tax", "/admin/reports/discounts", "/admin/data/role-menus", "/admin/data/user-menus", "/admin/data/menu-groups", "/admin/reports/cash-book", "/admin/reports/bank-book", "/admin/reports/customer-ledger", "/admin/reports/supplier-ledger", "/admin/reports/customer-segments", "/admin/reports/search-analytics" })
        {
            var r = await c.GetAsync(path);
            Assert.True(r.StatusCode == HttpStatusCode.OK, path + ": " + await r.Content.ReadAsStringAsync());
        }
    }

    [Fact]

    public async Task BarcodeLabelsRenderSvg()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var sku = await db.Set<Ecommerce.Domain.ProductSku>().FirstAsync();
        using var c = await fixture.AdminClient();
        var r = await c.GetAsync("/admin/files/barcode/" + sku.Id);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("image/svg+xml", r.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<svg", await r.Content.ReadAsStringAsync());
    }

    [Fact]

    public async Task ProductAndVariantCrudRoundTrip()
    {
        using var c = await fixture.AdminClient();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var category = await db.Set<Ecommerce.Domain.Category>().FirstAsync();
        var product = new Ecommerce.Domain.Product
        {
            Name = "CRUD product",
            Code = "CRUD-" + Guid.NewGuid().ToString("N"),
            Slug = "crud-" + Guid.NewGuid().ToString("N"),
            CategoryId = category.Id
        };
        async Task Save(string module, Ecommerce.Domain.Entity entity)
        {
            var page = await c.GetStringAsync("/admin/data/" + module + "/edit");
            var fields = Ecommerce.Web.MasterModules.Fields(entity.GetType()).ToDictionary(x => x.Name, x => x.PropertyType == typeof(bool) ? ((bool)x.GetValue(entity)!).ToString().ToLowerInvariant() : Convert.ToString(x.GetValue(entity), System.Globalization.CultureInfo.InvariantCulture) ?? "");
            fields["id"] = entity.Id.ToString();
            fields["version"] = Convert.ToBase64String(entity.Version);
            fields["__RequestVerificationToken"] = WebFixture.Token(page);
            var response = await c.PostAsync("/admin/data/" + module + "/save", new FormUrlEncodedContent(fields));
            Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        }
        await Save("products", product);
        var stored = await db.Set<Ecommerce.Domain.Product>().AsNoTracking().SingleAsync(x => x.Slug == product.Slug);
        stored.Name = "Edited product";
        await Save("products", stored);
        Assert.Equal("Edited product", await db.Set<Ecommerce.Domain.Product>().Where(x => x.Id == stored.Id).Select(x => x.Name).SingleAsync());
        var variant = new Ecommerce.Domain.ProductSku
        {
            Name = "Variant",
            ProductId = stored.Id,
            SKU = "SKU-" + Guid.NewGuid().ToString("N"),
            SalePrice = 99
        };
        await Save("variants", variant);
        Assert.True(await db.Set<Ecommerce.Domain.ProductSku>().AnyAsync(x => x.ProductId == stored.Id && x.SalePrice == 99));
        var html = await c.GetStringAsync("/admin/data/products");
        var archived = await c.PostAsync("/admin/data/products/archive", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", stored.Id.ToString() },
            { "__RequestVerificationToken", WebFixture.Token(html) }
        }));
        Assert.Equal(HttpStatusCode.Redirect, archived.StatusCode);
        Assert.False(await db.Set<Ecommerce.Domain.Product>().Where(x => x.Id == stored.Id).Select(x => x.Active).SingleAsync());
    }

    [Fact]

    public async Task GuestCheckoutUsesIsolatedIdentityAndCannotReadAnotherOrder()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var sku = await db.Set<Ecommerce.Domain.ProductSku>().FirstAsync(x => x.Active && x.Product.Active);
        var zone = await db.Set<Ecommerce.Domain.DeliveryZone>().FirstAsync();
        using var guest = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await guest.GetStringAsync("/cart");
        var cart = await guest.GetStringAsync("/products");
        var added = await guest.PostAsync("/cart/add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "skuId", sku.Id.ToString() },
            { "quantity", "1" },
            { "__RequestVerificationToken", WebFixture.Token(cart) }
        }));
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        var checkout = await guest.GetStringAsync("/checkout");
        var fields = new Dictionary<string,
        string>
        {
            {
                "Name",
                "Guest buyer"
            },
            {
                "Email",
                "test-admin@example.com"
            },
            {
                "Phone",
                "01000000000"
            },
            {
                "Address",
                "Guest delivery address"
            },
            {
                "ZoneId",
                zone.Id.ToString()
            },
            {
                "Method",
                "COD"
            },
            {
                "Key",
                Guid.NewGuid().ToString()
            },
            {
                "__RequestVerificationToken",
                WebFixture.Token(checkout)
            }
        };
        var review = await guest.PostAsync("/checkout/review", new FormUrlEncodedContent(fields));
        var html = await review.Content.ReadAsStringAsync();
        Assert.True(review.StatusCode == HttpStatusCode.OK, html);
        fields["__RequestVerificationToken"] = WebFixture.Token(html);
        var placed = await guest.PostAsync("/checkout", new FormUrlEncodedContent(fields));
        Assert.True(placed.StatusCode == HttpStatusCode.Redirect, await placed.Content.ReadAsStringAsync());
        Assert.StartsWith("/orders/", placed.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(placed.Headers.Location)).StatusCode);
        var order = await db.Set<Ecommerce.Domain.Order>().SingleAsync(x => x.CheckoutKey == fields["Key"]);
        var user = await db.Users.SingleAsync(x => x.Id == order.UserId);
        Assert.StartsWith("guest-", user.UserName);
        Assert.NotEqual("test-admin@example.com", user.Email);
        var admin = await guest.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Contains("/account/denied", admin.Headers.Location!.ToString());
        using var outsider = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        Assert.Equal(HttpStatusCode.Redirect, (await outsider.GetAsync(placed.Headers.Location)).StatusCode);
    }

    [BrowserFact]

    public async Task ThumbnailProducesPng()
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3X8AAAAASUVORK5CYII=");
        var thumbnail = await Ecommerce.Web.ProductMedia.ThumbnailAsync(bytes, "image/png");
        Assert.True(thumbnail.Length > 100);
        Assert.Equal(137, thumbnail[0]);
    }

    [BrowserFact]

    public async Task PdfExportProducesPdfBytes()
    {
        using var c = await fixture.AdminClient();
        var response = await c.GetAsync("/admin/reports/products?format=pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
        if (Environment.GetEnvironmentVariable("ERP_UI_CAPTURE") is { Length: > 0 } output) await File.WriteAllBytesAsync(Path.Combine(output, "products-report.pdf"), bytes);
    }
}

public class BrowserFactAttribute : FactAttribute
{

    public BrowserFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ERP_BROWSER_TESTS") != "1") Skip = "Set ERP_BROWSER_TESTS=1 after installing Chromium to run PDF rendering test.";
    }
}
