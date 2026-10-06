using Ecommerce.Application;
using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Ecommerce.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;





if (args.Contains("--install-pdf")) { Environment.ExitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" }); return; }
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false).AddEnvironmentVariables();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(o => { o.Password.RequiredLength = 12; o.Password.RequireNonAlphanumeric = true; o.User.RequireUniqueEmail = true; o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); }).AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/account/login";
    o.AccessDeniedPath = "/account/denied";

    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.Path = "/";
    o.Cookie.Domain = null;

    o.ExpireTimeSpan = TimeSpan.FromHours(4);
});
if (builder.Configuration["DataProtection:Path"] is { Length: > 0 } keyPath) { Directory.CreateDirectory(keyPath); builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath)).SetApplicationName("ParuEcommerceERP"); }
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<BusinessRulesService>(); builder.Services.AddScoped<ProcurementService>(); builder.Services.AddScoped<ReconciliationService>(); builder.Services.AddHttpClient<RefundGatewayService>(h => h.Timeout = TimeSpan.FromSeconds(45)); builder.Services.AddHostedService<EmailOutboxWorker>(); builder.Services.AddScoped<AccessService>(); builder.Services.AddScoped<BrandingService>(); builder.Services.AddScoped<LedgerService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<CartService>(); builder.Services.AddScoped<OrderService>(); builder.Services.AddScoped<PurchaseService>(); builder.Services.AddScoped<ReturnService>(); builder.Services.AddScoped<PaymentService>(); builder.Services.AddScoped<PaymentGatewayFactory>(); builder.Services.AddScoped<SeedData>();
builder.Services.AddHttpClient<SslCommerzGateway>(h => h.Timeout = TimeSpan.FromSeconds(45)); builder.Services.AddScoped<IPaymentGateway>(p => p.GetRequiredService<SslCommerzGateway>()); builder.Services.AddScoped<IPaymentGateway, CodGateway>();

// Local Ollama HTTP client
builder.Services.AddHttpClient<OllamaService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Ollama:Local:BaseUrl"]
        ?? "http://localhost:11434/");

    client.Timeout = TimeSpan.FromSeconds(120);
});

// Hybrid service depends on OllamaService, not HttpClient
builder.Services.AddScoped<IAIService, HybridOllamaService>();

// Never log HTTP request URLs for payment clients: validation URLs include merchant credentials.
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.None);
builder.Services.AddScoped<INotificationService, NotificationService>(); builder.Services.AddScoped<ICourierProvider, LocalCourierProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>(); builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("account", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) })); o.AddPolicy("ai", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.Identity?.Name ?? "anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) })); });




var baseConnection =
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is missing.");


// Read the exact keys saved in User Secrets
var dbUser = builder.Configuration["Database:UserId"];
var dbPassword = builder.Configuration["Database:Password"];

if (string.IsNullOrWhiteSpace(dbUser))
    throw new InvalidOperationException(
        "User Secrets: Database:UserId is missing.");

if (string.IsNullOrWhiteSpace(dbPassword))
    throw new InvalidOperationException(
        "User Secrets: Database:Password is missing.");

var connectionBuilder =
    new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(baseConnection)
    {
        IntegratedSecurity = false,
        UserID = dbUser,
        Password = dbPassword
    };

var connectionString = connectionBuilder.ConnectionString;



builder.Services.AddDbContext<AppDb>(options =>
{
    options.UseSqlServer(connectionString);
});


var app = builder.Build();

// ======================================================
// DEVELOPMENT ERROR DETAILS
// ======================================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    Console.WriteLine("========================================");
    Console.WriteLine($"Environment : {app.Environment.EnvironmentName}");
    Console.WriteLine("Application : ParuEcommerceERP");
    Console.WriteLine("========================================");
}


// ======================================================
// MIGRATION / SEED COMMAND
// ======================================================
if (args.Contains("--migrate") || args.Contains("--seed") || args.Contains("--seed-demo"))
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    if (args.Contains("--migrate"))
    {
        await db.Database.MigrateAsync();
    }

    if (args.Contains("--seed"))
    {
        await scope.ServiceProvider
            .GetRequiredService<SeedData>()
            .RunAsync();
    }

    return;
}


// ======================================================
// NORMAL APPLICATION STARTUP SEED
// ======================================================
// Safe to run repeatedly because SeedData checks existing rows.
// This makes roles/admin/menu/menu-items available after a normal
// Visual Studio or IIS application start; --seed is not required.
using (var seedScope = app.Services.CreateScope())
{
    try
    {
        var seedData = seedScope.ServiceProvider.GetRequiredService<SeedData>();
        await seedData.RunAsync();

        app.Logger.LogInformation(
            "Startup seed completed: roles, admin, menus and master data checked.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(
            ex,
            "Startup seed failed. Application startup stopped to avoid running with incomplete menu/security data.");

        throw;
    }
}


// ======================================================
// PRODUCTION SECURITY
// ======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ======================================================
// SECURITY HEADERS + GLOBAL ERROR HANDLER
// ======================================================
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] =
        "strict-origin-when-cross-origin";

    try
    {
        await next();
    }
    catch (BusinessException ex)
    {
        if (ctx.Response.HasStarted)
            throw;

        ctx.Response.Clear();
        ctx.Response.StatusCode =
            StatusCodes.Status400BadRequest;

        ctx.Response.ContentType =
            "text/plain; charset=utf-8";

        await ctx.Response.WriteAsync(ex.Message);
    }
    catch (DbUpdateConcurrencyException ex)
    {
        app.Logger.LogWarning(
            ex,
            "Database concurrency error. TraceId: {TraceId}",
            ctx.TraceIdentifier);

        if (ctx.Response.HasStarted)
            throw;

        ctx.Response.Clear();
        ctx.Response.StatusCode =
            StatusCodes.Status409Conflict;

        ctx.Response.ContentType =
            "text/plain; charset=utf-8";

        await ctx.Response.WriteAsync(
            "The record changed. Reload and try again.");
    }
    catch (Exception ex)
    {
        // IMPORTANT:
        // Log the REAL exception, not only the TraceId.
        app.Logger.LogError(
            ex,
            "Unhandled error. TraceId: {TraceId}, Path: {Path}",
            ctx.TraceIdentifier,
            ctx.Request.Path);

        if (ctx.Response.HasStarted)
            throw;

        ctx.Response.Clear();

        ctx.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        ctx.Response.ContentType =
            "text/plain; charset=utf-8";

        // Full error ONLY on local Development environment.
        if (app.Environment.IsDevelopment())
        {
            await ctx.Response.WriteAsync(
                "PARU ECOMMERCE ERP - FULL ERROR\n" +
                "========================================\n\n" +

                $"Trace ID:\n{ctx.TraceIdentifier}\n\n" +

                $"Request Path:\n{ctx.Request.Path}\n\n" +

                $"Exception Type:\n" +
                $"{ex.GetType().FullName}\n\n" +

                $"Message:\n" +
                $"{ex.Message}\n\n" +

                $"Inner Exception:\n" +
                $"{ex.InnerException?.Message ?? "None"}\n\n" +

                $"Stack Trace:\n" +
                $"{ex.StackTrace}\n\n" +

                "========================================\n");
        }
        else
        {
            // Never expose stack trace publicly.
            await ctx.Response.WriteAsync(
                "Request could not be completed. Reference: " +
                ctx.TraceIdentifier);
        }
    }
});

// ======================================================
// STATIC FILES
// ======================================================
app.UseForwardedHeaders();
app.UseStaticFiles();

// ======================================================
// ROUTING
// ======================================================
app.UseRouting();

// ======================================================
// AUTHENTICATION
// ======================================================
app.UseAuthentication();

// ======================================================
// SET CURRENT USER FOR DATABASE AUDIT
// ======================================================
app.Use(async (ctx, next) =>
{
    var db = ctx.RequestServices.GetRequiredService<AppDb>();

    db.CurrentActor =
        ctx.User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier
        )?.Value ?? "anonymous";

    await next();
});

// ======================================================
// AUTHORIZATION
// ======================================================
app.UseAuthorization();

// ======================================================
// RATE LIMITER
// ======================================================
app.UseRateLimiter();

// ======================================================
// HEALTH CHECK
// ======================================================
app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "running",
        application = "ParuEcommerceERP",
        environment = app.Environment.EnvironmentName
    }));

// ======================================================
// DATABASE HEALTH CHECK
// LOCAL DIAGNOSTIC
// ======================================================
app.MapGet("/health/database", async (AppDb db) =>
{
    try
    {
        bool connected =
            await db.Database.CanConnectAsync();

        if (connected)
        {
            return Results.Ok(new
            {
                status = "connected",
                database = "ParuEcommerceERP"
            });
        }

        return Results.Problem(
            "SQL Server could not be reached.");
    }
    catch (Exception ex)
    {
        // Full details only during Development.
        if (app.Environment.IsDevelopment())
        {
            return Results.Problem(
                detail:
                    ex.Message +
                    "\n\nInner Exception:\n" +
                    ex.InnerException?.Message,
                title: "Database Connection Failed",
                statusCode: 500);
        }

        return Results.Problem(
            "Database connection failed.");
    }
});

// ======================================================
// MVC ROUTE
// ======================================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Store}/{action=Index}/{id?}"
);

// ======================================================
// START APPLICATION
// ======================================================
app.Run();

public partial class Program
{
}
