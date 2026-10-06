using Ecommerce.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace Ecommerce.Infrastructure;

public class SeedData(AppDb db, UserManager<IdentityUser> users, RoleManager<IdentityRole> roles, IConfiguration config)
{



    public async Task RunAsync()
    {
        // PRODUCTION-SAFE STARTUP SEED:
        // only security + navigation data.
        await SeedRolesAsync();
        await SeedAdminAsync();
        await SeedPermissionsAsync();
        await SeedMenusAsync();
    }

    public async Task RunDemoAsync()
    {
        // Explicit/manual demo/master-data seed only.
        // Never called automatically by normal IIS startup.
        await SeedRolesAsync();
        var admin = await SeedAdminAsync();
        await SeedPermissionsAsync();
        await SeedMenusAsync();
        await SeedMasterAndDemoDataAsync(admin);
    }

    private async Task SeedRolesAsync()
    {
        string[] roleNames = ["SuperAdmin", "Admin", "Manager", "Sales", "Purchase", "Inventory", "StoreOfficer", "Accountant", "CustomerCare", "DeliveryManager", "ContentManager", "Customer"];
        foreach (var role in roleNames) if (!await roles.RoleExistsAsync(role)) { var result = await roles.CreateAsync(new IdentityRole(role)); if (!result.Succeeded) throw new InvalidOperationException(string.Join(";", result.Errors.Select(x => x.Description))); }
    }

    private async Task<IdentityUser> SeedAdminAsync()
    {
        // Existing live user is used. No user is created or deleted here.
        var userName = config["Seed:AdminUserName"];

        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new InvalidOperationException(
                "Seed:AdminUserName is missing. Add the existing admin UserName to appsettings.Local.json or an environment variable.");
        }

        var admin = await users.FindByNameAsync(userName);

        if (admin == null)
        {
            throw new InvalidOperationException(
                $"Admin user '{userName}' was not found in AspNetUsers. Check SELECT Id, UserName FROM AspNetUsers.");
        }

        // SeedRolesAsync runs first, therefore both roles already exist here.
        foreach (var roleName in new[] { "Admin", "SuperAdmin" })
        {
            if (await users.IsInRoleAsync(admin, roleName))
                continue;

            var result = await users.AddToRoleAsync(admin, roleName);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign '{roleName}' to '{userName}': " +
                    string.Join("; ", result.Errors.Select(x => x.Description)));
            }
        }

        return admin;
    }

    private async Task SeedPermissionsAsync()
    {
        string[] modules = ["dashboard", "catalog", "orders", "payments", "inventory", "purchase", "returns", "delivery", "support", "accounts", "reports", "ai", "security"];
        string[] actions = ["view", "create", "edit", "delete", "approve", "cancel", "print", "export", "refund", "admin", "manage"];
        if (!await db.Set<PermissionGrant>().AnyAsync())
        {
            foreach (var module in modules) foreach (var action in actions) db.Add(new PermissionGrant { Role = "Admin", Permission = module + "." + action });
            var mapping = new Dictionary<string, string[]> { ["Manager"] = ["dashboard.view", "catalog.view", "orders.view", "orders.edit", "inventory.view", "reports.view", "reports.export", "purchase.view", "ai.admin"], ["Sales"] = ["dashboard.view", "catalog.view", "orders.view", "orders.edit", "orders.create", "payments.edit"], ["Purchase"] = ["dashboard.view", "catalog.view", "purchase.view", "purchase.create", "purchase.edit"], ["Inventory"] = ["dashboard.view", "inventory.view", "inventory.edit"], ["StoreOfficer"] = ["dashboard.view", "inventory.view"], ["Accountant"] = ["dashboard.view", "accounts.view", "accounts.edit", "accounts.approve", "reports.view", "reports.export", "payments.edit", "payments.refund", "returns.view"], ["CustomerCare"] = ["dashboard.view", "support.view", "support.edit", "orders.view", "returns.view"], ["DeliveryManager"] = ["dashboard.view", "orders.view", "orders.edit", "delivery.edit"], ["ContentManager"] = ["dashboard.view", "catalog.view", "catalog.create", "catalog.edit"] };
            foreach (var m in mapping) foreach (var p in m.Value) db.Add(new PermissionGrant { Role = m.Key, Permission = p });
        }

        await db.SaveChangesAsync();
    }

    private async Task SeedMenusAsync()
    {
        // =====================================================
        // 1. MENU GROUPS
        // =====================================================

        var menuGroups = new[]
        {
        ("Dashboard", 10),
        ("Catalog", 20),
        ("Marketing", 30),
        ("Sales", 40),
        ("Inventory", 50),
        ("Purchase", 60),
        ("Delivery", 70),
        ("Customer Service", 80),
        ("Accounts", 90),
        ("Reports", 100),
        ("Security", 110),
        ("Organization", 120),
        ("Configuration", 130),
        ("AI", 140)
    };


        // =====================================================
        // 2. CREATE / UPDATE MENU GROUPS
        // =====================================================

        foreach (var g in menuGroups)
        {
            // শুধু প্রয়োজনীয় field select করছি।
            // Existing legacy row-এর অন্য nullable column-এর কারণে
            // SqlNullValueException হওয়ার ঝুঁকি কমবে।
            var existing = await db.Set<Menu>()
                .AsNoTracking()
                .Where(x => x.Name == g.Item1)
                .Select(x => new
                {
                    x.Id
                })
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                var menu = new Menu
                {
                    Id = Guid.NewGuid(),
                    Name = g.Item1,
                    SortOrder = g.Item2,
                    Active = true,

                    CreatedBy = "SYSTEM",
                    UpdatedBy = "SYSTEM",

                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.Set<Menu>().Add(menu);
            }
            else
            {
                // Attach না করে direct database update
                await db.Set<Menu>()
                    .Where(x => x.Id == existing.Id)
                    .ExecuteUpdateAsync(setters => setters

                        .SetProperty(x => x.SortOrder, g.Item2)

                        .SetProperty(x => x.Active, true)

                        .SetProperty(
                            x => x.UpdatedBy,
                            "SYSTEM")

                        .SetProperty(
                            x => x.UpdatedAt,
                            DateTime.UtcNow));
            }
        }

        await db.SaveChangesAsync();


        // =====================================================
        // 3. GET MENU IDS
        // =====================================================

        var menuNames = menuGroups
            .Select(x => x.Item1)
            .ToArray();

        var groupIds = await db.Set<Menu>()
            .AsNoTracking()
            .Where(x => menuNames.Contains(x.Name))
            .Select(x => new
            {
                x.Name,
                x.Id
            })
            .ToDictionaryAsync(
                x => x.Name,
                x => x.Id);


        // =====================================================
        // 4. SAFETY CHECK
        // =====================================================

        foreach (var g in menuGroups)
        {
            if (!groupIds.ContainsKey(g.Item1))
            {
                throw new InvalidOperationException(
                    $"Menu group '{g.Item1}' was not created.");
            }
        }


        // =====================================================
        // 5. MENU ITEMS
        // =====================================================

        var menuDefinitions =
            new (string Group,
                 string Label,
                 string Url,
                 string Permission,
                 string Icon,
                 int Sort)[]
        {
        // ---------------------------------------------
        // DASHBOARD
        // ---------------------------------------------

        ("Dashboard",
         "Overview",
         "/admin",
         "dashboard.view",
         "bi bi-speedometer2",
         10),


        // ---------------------------------------------
        // CATALOG
        // ---------------------------------------------

        ("Catalog",
         "Products",
         "/admin/data/products",
         "catalog.view",
         "bi bi-box-seam",
         10),

        ("Catalog",
         "Variants / SKU",
         "/admin/data/variants",
         "catalog.view",
         "bi bi-upc-scan",
         20),

        ("Catalog",
         "Categories",
         "/admin/data/categories",
         "catalog.view",
         "bi bi-grid",
         30),

        ("Catalog",
         "Brands",
         "/admin/data/brands",
         "catalog.view",
         "bi bi-award",
         40),

        ("Catalog",
         "Units",
         "/admin/data/units",
         "catalog.view",
         "bi bi-rulers",
         50),


        // ---------------------------------------------
        // MARKETING
        // ---------------------------------------------

        ("Marketing",
         "Banners",
         "/admin/data/banners",
         "catalog.view",
         "bi bi-image",
         10),

        ("Marketing",
         "Campaigns",
         "/admin/data/campaigns",
         "catalog.view",
         "bi bi-megaphone",
         20),

        ("Marketing",
         "Coupons",
         "/admin/data/coupons",
         "catalog.view",
         "bi bi-ticket-perforated",
         30),


        // ---------------------------------------------
        // SALES
        // ---------------------------------------------

        ("Sales",
         "Orders & Delivery",
         "/admin/operations/orders",
         "orders.view",
         "bi bi-bag-check",
         10),

        ("Sales",
         "Point of Sale",
         "/admin/pos",
         "orders.create",
         "bi bi-cart-check",
         20),

        ("Sales",
         "Returns & Refunds",
         "/admin/operations/returns",
         "returns.view",
         "bi bi-arrow-counterclockwise",
         30),


        // ---------------------------------------------
        // INVENTORY
        // ---------------------------------------------

        ("Inventory",
         "Inventory",
         "/admin/operations/inventory",
         "inventory.view",
         "bi bi-boxes",
         10),

        ("Inventory",
         "Warehouses",
         "/admin/data/warehouses",
         "inventory.view",
         "bi bi-building",
         20),

        ("Inventory",
         "Stores",
         "/admin/data/stores",
         "inventory.view",
         "bi bi-shop",
         30),


        // ---------------------------------------------
        // PURCHASE
        // ---------------------------------------------

        ("Purchase",
         "Purchasing",
         "/admin/operations/purchases",
         "purchase.view",
         "bi bi-receipt",
         10),

        ("Purchase",
         "Suppliers",
         "/admin/data/suppliers",
         "purchase.view",
         "bi bi-people",
         20),


        // ---------------------------------------------
        // DELIVERY
        // ---------------------------------------------

        ("Delivery",
         "Delivery Methods",
         "/admin/data/delivery-methods",
         "delivery.edit",
         "bi bi-truck",
         10),

        ("Delivery",
         "Delivery Zones",
         "/admin/data/delivery-zones",
         "delivery.edit",
         "bi bi-geo-alt",
         20),

        ("Delivery",
         "Districts",
         "/admin/data/districts",
         "delivery.edit",
         "bi bi-map",
         30),

        ("Delivery",
         "Delivery Areas",
         "/admin/data/delivery-areas",
         "delivery.edit",
         "bi bi-pin-map",
         40),


        // ---------------------------------------------
        // CUSTOMER SERVICE
        // ---------------------------------------------

        ("Customer Service",
         "Support & Reviews",
         "/admin/operations/support",
         "support.view",
         "bi bi-headset",
         10),


        // ---------------------------------------------
        // ACCOUNTS
        // ---------------------------------------------

        ("Accounts",
         "Accounts & Expenses",
         "/admin/operations/accounts",
         "accounts.view",
         "bi bi-cash-stack",
         10),

        ("Accounts",
         "Account Heads",
         "/admin/data/accounts",
         "accounts.view",
         "bi bi-journal-text",
         20),

        ("Accounts",
         "Expense Categories",
         "/admin/data/expense-categories",
         "accounts.view",
         "bi bi-wallet2",
         30),


        // ---------------------------------------------
        // REPORTS
        // ---------------------------------------------

        ("Reports",
         "All Reports",
         "/admin/reports",
         "reports.view",
         "bi bi-bar-chart-line",
         10),

        ("Reports",
         "Sales Report",
         "/admin/reports/sales",
         "reports.view",
         "bi bi-graph-up-arrow",
         20),

        ("Reports",
         "Order Report",
         "/admin/reports/orders",
         "reports.view",
         "bi bi-receipt",
         30),

        ("Reports",
         "Stock Report",
         "/admin/reports/stock",
         "reports.view",
         "bi bi-box-seam",
         40),

        ("Reports",
         "Stock Ledger",
         "/admin/reports/stock-ledger",
         "reports.view",
         "bi bi-journal-arrow-down",
         50),

        ("Reports",
         "Low Stock",
         "/admin/reports/low-stock",
         "reports.view",
         "bi bi-exclamation-triangle",
         60),

        ("Reports",
         "Inventory Valuation",
         "/admin/reports/inventory-valuation",
         "reports.view",
         "bi bi-calculator",
         70),

        ("Reports",
         "Purchase Report",
         "/admin/reports/purchases",
         "reports.view",
         "bi bi-bag-plus",
         80),

        ("Reports",
         "Payments",
         "/admin/reports/payments",
         "reports.view",
         "bi bi-credit-card",
         90),

        ("Reports",
         "Customers",
         "/admin/reports/customers",
         "reports.view",
         "bi bi-people",
         100),

        ("Reports",
         "Profit & Loss",
         "/admin/reports/profit-loss",
         "reports.view",
         "bi bi-cash-coin",
         110),

        ("Reports",
         "Balance Sheet",
         "/admin/reports/balance-sheet",
         "reports.view",
         "bi bi-file-spreadsheet",
         120),

        ("Reports",
         "Trial Balance",
         "/admin/reports/trial-balance",
         "reports.view",
         "bi bi-list-columns",
         130),

        ("Reports",
         "Cash Book",
         "/admin/reports/cash-book",
         "reports.view",
         "bi bi-cash-stack",
         140),

        ("Reports",
         "Bank Book",
         "/admin/reports/bank-book",
         "reports.view",
         "bi bi-bank",
         150),

        ("Reports",
         "General Ledger",
         "/admin/reports/general-ledger",
         "reports.view",
         "bi bi-journal-bookmark",
         160),

        ("Reports",
         "KPI Dashboard",
         "/admin/reports/kpi",
         "reports.view",
         "bi bi-speedometer",
         170),


        // ---------------------------------------------
        // SECURITY
        // ---------------------------------------------

        ("Security",
         "Users & Roles",
         "/admin/security",
         "security.manage",
         "bi bi-person-gear",
         10),

        ("Security",
         "Permissions",
         "/admin/data/permissions",
         "security.manage",
         "bi bi-shield-check",
         20),

        ("Security",
         "Dynamic Menus",
         "/admin/data/menus",
         "security.manage",
         "bi bi-list-nested",
         30),

        ("Security",
         "Role Menu Rules",
         "/admin/data/role-menus",
         "security.manage",
         "bi bi-person-check",
         40),

        ("Security",
         "User Menu Rules",
         "/admin/data/user-menus",
         "security.manage",
         "bi bi-person-lock",
         50),


        // ---------------------------------------------
        // ORGANIZATION
        // ---------------------------------------------

        ("Organization",
         "Companies",
         "/admin/data/companies",
         "security.manage",
         "bi bi-buildings",
         10),

        ("Organization",
         "Branches",
         "/admin/data/branches",
         "security.manage",
         "bi bi-diagram-2",
         20),


        // ---------------------------------------------
        // CONFIGURATION
        // ---------------------------------------------

        ("Configuration",
         "Currencies",
         "/admin/data/currencies",
         "security.manage",
         "bi bi-currency-exchange",
         10),

        ("Configuration",
         "Taxes",
         "/admin/data/taxes",
         "security.manage",
         "bi bi-percent",
         20),

        ("Configuration",
         "Fiscal Years",
         "/admin/data/fiscal-years",
         "security.manage",
         "bi bi-calendar-range",
         30),

        ("Configuration",
         "Payment Gateways",
         "/admin/data/payment-gateways",
         "security.manage",
         "bi bi-credit-card",
         40),

        ("Configuration",
         "Business Settings",
         "/admin/data/settings",
         "security.manage",
         "bi bi-gear",
         50),


        // ---------------------------------------------
        // AI
        // ---------------------------------------------

        ("AI",
         "AI Assistant",
         "/assistant",
         "ai.admin",
         "bi bi-stars",
         10)
        };


        // =====================================================
        // 6. CREATE / UPDATE MENU ITEMS
        // =====================================================

        foreach (var m in menuDefinitions)
        {
            var existing = await db.Set<MenuItem>()
                .AsNoTracking()
                .Where(x => x.Url == m.Url)
                .Select(x => new
                {
                    x.Id
                })
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                var item = new MenuItem
                {
                    Id = Guid.NewGuid(),

                    MenuId = groupIds[m.Group],

                    Label = m.Label,
                    Url = m.Url,
                    Permission = m.Permission,

                    Icon = m.Icon,
                    SortOrder = m.Sort,

                    CreatedBy = "SYSTEM",
                    UpdatedBy = "SYSTEM",

                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow

                    // Version দেবেন না।
                    // SQL Server timestamp/rowversion নিজে করবে।
                };

                db.Set<MenuItem>().Add(item);
            }
            else
            {
                await db.Set<MenuItem>()
                    .Where(x => x.Id == existing.Id)
                    .ExecuteUpdateAsync(setters => setters

                        .SetProperty(
                            x => x.MenuId,
                            groupIds[m.Group])

                        .SetProperty(
                            x => x.Label,
                            m.Label)

                        .SetProperty(
                            x => x.Url,
                            m.Url)

                        .SetProperty(
                            x => x.Permission,
                            m.Permission)

                        .SetProperty(
                            x => x.Icon,
                            m.Icon)

                        .SetProperty(
                            x => x.SortOrder,
                            m.Sort)

                        .SetProperty(
                            x => x.UpdatedBy,
                            "SYSTEM")

                        .SetProperty(
                            x => x.UpdatedAt,
                            DateTime.UtcNow));
            }
        }


        // =====================================================
        // 7. SAVE NEW MENU ITEMS
        // =====================================================

        await db.SaveChangesAsync();
    }

    private async Task SeedMasterAndDemoDataAsync(IdentityUser admin)
    {
        if (!await db.Set<AccountHead>().AnyAsync()) foreach (var a in new[] { ("1000", "Cash", "Asset"), ("1010", "Gateway clearing", "Asset"), ("1100", "Customer receivables", "Asset"), ("1200", "Inventory", "Asset"), ("2000", "Supplier payables", "Liability"), ("2100", "VAT payable", "Liability"), ("3100", "Opening equity / adjustments", "Equity"), ("4000", "Product sales", "Revenue"), ("4100", "Shipping income", "Revenue"), ("4200", "Sales returns", "ContraRevenue"), ("5000", "Cost of goods sold", "Expense"), ("6000", "Operating expenses", "Expense") }) db.Add(new AccountHead { Code = a.Item1, Name = a.Item2, Type = a.Item3 });
        await using var seedTransaction = await db.Database.BeginTransactionAsync();
        if (!await db.Set<Company>().AnyAsync())
        {
            var company = new Company { Name = "Paru Collection", Address = "Dhaka, Bangladesh" }; var branch = new Branch { Name = "Dhaka Main", CompanyId = company.Id }; var warehouse = new Warehouse { Name = "Central warehouse", BranchId = branch.Id }; var second = new Warehouse { Name = "Outlet warehouse", BranchId = branch.Id }; db.AddRange(company, branch, warehouse, second, new Store { Name = "Main store", WarehouseId = warehouse.Id }, new Currency { Name = "Bangladeshi Taka", Code = "BDT" }, new TaxRate { Name = "Zero VAT", Percent = 0 }, new FiscalYear { Name = "2026–2027", StartDate = new DateTime(2026, 7, 1), EndDate = new DateTime(2027, 6, 30) }, new Supplier { Name = "Dhaka Wholesale Supply", Phone = "01000000000", Address = "Demo supplier, Dhaka" }, new DeliveryZone { Name = "Dhaka city", District = "Dhaka", Charge = 60, FreeAbove = 3000 }, new DeliveryZone { Name = "Outside Dhaka", District = "Other", Charge = 120, FreeAbove = 5000 }, new DeliveryAgent { Name = "Own delivery team" }, new Coupon { Name = "Welcome offer", Code = "WELCOME10", Percent = 10, MinimumPurchase = 500, MaximumDiscount = 300, StartsAt = DateTime.UtcNow.AddDays(-1), EndsAt = DateTime.UtcNow.AddYears(1) }, new ExpenseCategory { Name = "Office and utilities" });
            var brand = new Brand { Name = "Paru Essentials", Slug = "paru-essentials" }; var unit = new Unit { Name = "Piece", Symbol = "pcs" }; db.AddRange(brand, unit);
            string[] names = ["Ladies Corner", "Men's Corner", "Kids Corner", "Cosmetics", "Kitchen Ware", "Toys", "Electronics"];
            string[] slugs = ["ladies", "mens", "kids", "cosmetics", "kitchen", "toys", "electronics"];
            string[][] items = [["Cotton Kurti", "Everyday Tote"], ["Classic Shirt", "Cotton Polo"], ["Kids T-Shirt", "School Backpack"], ["Moisturizer", "Lip Color"], ["Ceramic Mug", "Storage Jar"], ["Building Blocks", "Plush Bear"], ["Wireless Mouse", "Desk Lamp"]];
            for (int i = 0; i < names.Length; i++) { var cat = new Category { Name = names[i], Slug = slugs[i], SortOrder = i, ImagePath = "/images/sample-" + i + ".svg" }; db.Add(cat); for (int j = 0; j < 2; j++) { var p = new Product { Name = items[i][j], Code = $"PRD-{i + 1:00}{j + 1:00}", Slug = slugs[i] + "-" + (j + 1), CategoryId = cat.Id, BrandId = brand.Id, UnitId = unit.Id, Description = "Practice catalogue item. Replace descriptions, images and prices with your own verified product data.", Featured = j == 0, NewArrival = true }; var s = new ProductSku { Name = "Standard", ProductId = p.Id, SKU = p.Code + "-STD", Barcode = $"880000{i:00}{j:00}", PurchasePrice = 250 + i * 50, RegularPrice = 450 + i * 100, SalePrice = 399 + i * 100, Color = i < 3 ? "Blue" : "", Size = i < 3 ? "M" : "", ReorderLevel = 5 }; p.Skus.Add(s); p.Files.Add(new ProductFile { FileName = "sample-" + i + ".svg", FilePath = "/images/sample-" + i + ".svg", FileType = "image/svg+xml", IsPrimary = true }); db.Add(p); db.Add(new StockBalance { WarehouseId = warehouse.Id, SkuId = s.Id, OnHand = 30, AverageCost = s.PurchasePrice }); db.Add(new StockMovement { WarehouseId = warehouse.Id, SkuId = s.Id, Quantity = 30, UnitCost = s.PurchasePrice, Kind = "Opening", Reference = "DEMO-SEED", ActorId = admin.Id }); } }
            db.Add(new BusinessSetting { Name = "DisplayName", Value = "Paru Collection", Group = "Brand" });
            await db.SaveChangesAsync();
            var opening = (await db.Set<StockMovement>().Where(x => x.Reference == "DEMO-SEED").ToListAsync()).Sum(x => x.Quantity * x.UnitCost); var ledger = new LedgerService(db); await ledger.PostAsync("OPENING-DEMO", "Demo opening inventory", ("1200", opening, 0, ""), ("3100", 0, opening, ""));
        }
        if (!await db.Set<PaymentGateway>().AnyAsync()) db.AddRange(new PaymentGateway { Name = "Cash on delivery", ProviderCode = "COD", Sandbox = false, Description = "Staff cash collection" }, new PaymentGateway { Name = "SSLCommerz", ProviderCode = "SSLCommerz", Description = "Enable only through secure server configuration after merchant acceptance" }, new PaymentGateway { Name = "bKash adapter", ProviderCode = "bKash", Active = false, Description = "Extension contract; direct provider not enabled" }, new PaymentGateway { Name = "Nagad adapter", ProviderCode = "Nagad", Active = false, Description = "Extension contract; direct provider not enabled" });
        if (!await db.Set<DeliveryMethod>().AnyAsync()) db.AddRange(new DeliveryMethod { Name = "Home delivery" }, new DeliveryMethod { Name = "Store pickup" });
        if (!await db.Set<AccountHead>().AnyAsync(x => x.Code == "2200")) db.Add(new AccountHead { Code = "2200", Name = "Store credit liability", Type = "Liability" });
        foreach (var setting in new[] { ("CompanyName", "Paru Collection"), ("CompanyAddress", "Dhaka, Bangladesh"), ("CompanyPhone", ""), ("LogoUrl", "/images/fallback.svg"), ("PrimaryColor", "#153f35"), ("AccentColor", "#d7ac67"), ("BackgroundColor", "#f7f8f4"), ("ReportAddress", "Dhaka, Bangladesh"), ("ReportFooter", "Generated by E-commerce ERP") }) if (!await db.Set<BusinessSetting>().AnyAsync(x => x.Name == setting.Item1)) db.Add(new BusinessSetting { Name = setting.Item1, Value = setting.Item2, Group = "Brand" });
        foreach (var setting in new[] { ("ReturnDays", "7"), ("DefaultWarehouseId", ""), ("GuestCheckoutEnabled", "true"), ("AllowSplitFulfillment", "true") }) if (!await db.Set<BusinessSetting>().AnyAsync(x => x.Name == setting.Item1)) db.Add(new BusinessSetting { Name = setting.Item1, Value = setting.Item2 });
        await db.SaveChangesAsync(); await seedTransaction.CommitAsync();
        if (config.GetValue<bool>("Seed:IncludeDemoOrders") && !await db.Set<Order>().AnyAsync())
        {
            const string customerEmail = "demo.customer@example.com"; var customer = await users.FindByEmailAsync(customerEmail);
            if (customer == null) { customer = new IdentityUser { UserName = customerEmail, Email = customerEmail, PhoneNumber = "01000000000", EmailConfirmed = true }; var result = await users.CreateAsync(customer, "Demo!" + Guid.NewGuid().ToString("N") + "aA9"); if (!result.Succeeded) throw new InvalidOperationException("Demo user creation failed."); await users.AddToRoleAsync(customer, "Customer"); }
            var wh = await db.Set<Warehouse>().OrderBy(x => x.CreatedAt).FirstAsync(); var sku = await db.Set<ProductSku>().FirstAsync(); var zone = await db.Set<DeliveryZone>().FirstAsync(); var led = new LedgerService(db); var stock = new StockService(db, led); var orderService = new OrderService(db, stock, led); var cart = new CartService(db);
            await cart.SetAsync(customer.Id, sku.Id, 2); var delivered = await orderService.CheckoutAsync(new Ecommerce.Application.CheckoutRequest(customer.Id, "Demo Customer", "01000000000", customerEmail, "Demo address, Dhaka", zone.Id, wh.Id, "", "COD", Guid.NewGuid().ToString(), "Demo"));
            foreach (var status in new[] { OrderStatus.Confirmed, OrderStatus.Processing, OrderStatus.Packed, OrderStatus.ReadyToShip, OrderStatus.Shipped, OrderStatus.OutForDelivery, OrderStatus.Delivered }) await orderService.TransitionAsync(delivered.Id, status, admin.Id);
            await orderService.CollectCodAsync(delivered.Id, admin.Id);
            await cart.SetAsync(customer.Id, sku.Id, 1); await orderService.CheckoutAsync(new Ecommerce.Application.CheckoutRequest(customer.Id, "Demo Customer", "01000000000", customerEmail, "Demo address, Dhaka", zone.Id, wh.Id, "", "COD", Guid.NewGuid().ToString(), "Demo"));


        }

    }
}
