using Ecommerce.Domain;
using Ecommerce.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class AppDb(DbContextOptions<AppDb> options):IdentityDbContext<IdentityUser>(options) {
 public string CurrentActor {get;set;}="system";
 protected override void OnModelCreating(ModelBuilder b){
  base.OnModelCreating(b);
  foreach(var t in typeof(Entity).Assembly.GetTypes().Where(t=>t.IsClass&&!t.IsAbstract&&typeof(Entity).IsAssignableFrom(t)))b.Entity(t);
  foreach(var e in b.Model.GetEntityTypes()){
   foreach(var p in e.GetProperties()) {
    if(p.ClrType==typeof(decimal)||p.ClrType==typeof(decimal?)){p.SetPrecision(18);p.SetScale(2);}
    if(p.ClrType==typeof(string)&&p.GetMaxLength()==null)p.SetMaxLength(2000);
   }
   foreach(var fk in e.GetForeignKeys())fk.DeleteBehavior=DeleteBehavior.Restrict;
  }
  b.Entity<CampaignUse>().HasIndex(x=>new{x.CampaignId,x.OrderId}).IsUnique();
  b.Entity<CampaignUse>().HasIndex(x=>new{x.CampaignId,x.UserId});
  b.Entity<OrderAllocation>().HasIndex(x=>new{x.OrderLineId,x.WarehouseId}).IsUnique();
  b.Entity<OrderAllocation>().Property(x=>x.UnitCost).HasPrecision(18,4);
  b.Entity<SupplierInvoice>().HasIndex(x=>new{x.SupplierId,x.Number}).IsUnique();
  b.Entity<SupplierInvoiceLine>().HasIndex(x=>new{x.SupplierInvoiceId,x.PurchaseLineId}).IsUnique();
  b.Entity<StockBalance>().Property(x=>x.AverageCost).HasPrecision(18,4);
  b.Entity<OrderLine>().Property(x=>x.Cost).HasPrecision(18,4);
  b.Entity<StockMovement>().Property(x=>x.UnitCost).HasPrecision(18,4);
  b.Entity<GoodsReceipt>().HasIndex(x=>x.Reference).IsUnique();b.Entity<SupplierReturn>().HasIndex(x=>x.Reference).IsUnique();b.Entity<VariantAttribute>().HasIndex(x=>new{x.ProductSkuId,x.AttributeValueId}).IsUnique();b.Entity<ContentPage>().HasIndex(x=>x.Slug).IsUnique();b.Entity<BankStatementLine>().HasIndex(x=>x.JournalLineId).IsUnique().HasFilter("[JournalLineId] IS NOT NULL");
  b.Entity<Replacement>().HasIndex(x=>x.ReturnRequestId).IsUnique();
  b.Entity<RoleMenuPermission>().HasIndex(x=>new{x.Role,x.MenuItemId}).IsUnique();
  b.Entity<UserMenuPermission>().HasIndex(x=>new{x.UserId,x.MenuItemId}).IsUnique();
  b.Entity<ProductView>().HasIndex(x=>new{x.UserId,x.CreatedAt});
  b.Entity<ContentPage>().Property(x=>x.Body).HasMaxLength(20000);
  b.Entity<Category>().HasIndex(x=>x.Slug).IsUnique();b.Entity<Product>().HasIndex(x=>x.Slug).IsUnique();b.Entity<Product>().HasIndex(x=>x.Code).IsUnique();b.Entity<ProductSku>().HasIndex(x=>x.SKU).IsUnique();
  b.Entity<StockBalance>().HasIndex(x=>new{x.WarehouseId,x.SkuId}).IsUnique();
  b.Entity<StockBalance>().ToTable(t=>{t.HasCheckConstraint("CK_Stock_Nonnegative","[OnHand]>=0 AND [Reserved]>=0 AND [Reserved]<=[OnHand]");});
  b.Entity<CartItem>().HasIndex(x=>new{x.Owner,x.SkuId}).IsUnique();b.Entity<WishlistItem>().HasIndex(x=>new{x.UserId,x.ProductId}).IsUnique();
  b.Entity<Coupon>().HasIndex(x=>x.Code).IsUnique();b.Entity<Order>().HasIndex(x=>x.Number).IsUnique();b.Entity<Order>().HasIndex(x=>new{x.UserId,x.CheckoutKey}).IsUnique();
  b.Entity<PaymentTransaction>().HasIndex(x=>x.TransactionId).IsUnique();b.Entity<PaymentTransaction>().HasIndex(x=>x.GatewayTransactionId).IsUnique().HasFilter("[GatewayTransactionId] <> ''");
  b.Entity<PermissionGrant>().HasIndex(x=>new{x.Role,x.UserId,x.Permission}).IsUnique();
  b.Entity<AccountHead>().HasIndex(x=>x.Code).IsUnique();b.Entity<Journal>().HasIndex(x=>x.Reference).IsUnique();
  // Identity PK/FK strings must share a SQL Server-compatible size.
  foreach(var e in b.Model.GetEntityTypes().Where(x=>x.ClrType.Namespace=="Microsoft.AspNetCore.Identity"))foreach(var prop in (e.FindPrimaryKey()?.Properties??Array.Empty<Microsoft.EntityFrameworkCore.Metadata.IMutableProperty>()).Concat(e.GetForeignKeys().SelectMany(x=>x.Properties)).Distinct())if(prop.ClrType==typeof(string))prop.SetMaxLength(160);
  // Indexed text is bounded to keep composite keys within SQL Server limits.
  foreach(var e in b.Model.GetEntityTypes())foreach(var p in e.GetIndexes().SelectMany(i=>i.Properties))if(p.ClrType==typeof(string))p.SetMaxLength(160);
 }
 public override Task<int> SaveChangesAsync(CancellationToken ct=default){foreach(var e in ChangeTracker.Entries<Entity>()){if(e.State==EntityState.Added)e.Entity.CreatedBy=CurrentActor;if(e.State==EntityState.Modified){e.Entity.UpdatedAt=DateTime.UtcNow;e.Entity.UpdatedBy=CurrentActor;}}return base.SaveChangesAsync(ct);}
}
public class Repository<T>(AppDb db):IRepository<T> where T:Entity {
 public IQueryable<T> Query()=>db.Set<T>();public async Task<T?> FindAsync(Guid id)=>await db.Set<T>().FindAsync(id);public async Task AddAsync(T e)=>await db.Set<T>().AddAsync(e);public Task<int> SaveAsync()=>db.SaveChangesAsync();
}
