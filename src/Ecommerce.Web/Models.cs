using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Ecommerce.Domain;
namespace Ecommerce.Web;

public record CatalogVm(
    List<Product> Products,
    List<CategoryVm> Categories,
    string Search,
    Guid? Category,
    string Sort,
    int Page,
    bool More
);
public class CategoryVm
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool Active { get; set; }
}
public class LoginVm {[Required]public string Email {get;set;}="";[Required,DataType(DataType.Password)]public string Password {get;set;}="";public bool Admin {get;set;} }
public class StaffRegisterVm
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
}

public class RegisterVm {[Required,EmailAddress]public string Email {get;set;}="";[Required,MinLength(12),DataType(DataType.Password)]public string Password {get;set;}=""; public string Phone { get; set; } = ""; }
public class CheckoutVm {[Required]public string Name {get;set;}="";[Required]public string Phone {get;set;}="";[Required,EmailAddress]public string Email {get;set;}="";[Required]public string Address {get;set;}="";public Guid ZoneId {get;set;}public string Coupon {get;set;}="";public string Method {get;set;}="COD";public string Key {get;set;}=Guid.NewGuid().ToString();}
public record TableVm(string Title,string[] Columns,List<string[]> Rows,string? Module=null);
public record EditorVm(string Module,Entity Entity,PropertyInfo[] Fields,Dictionary<string,List<(string Id,string Label)>> Choices);
public static class MasterModules {
 public static readonly Dictionary<string,Type> Types=new(StringComparer.OrdinalIgnoreCase){
 ["delivery-methods"]=typeof(DeliveryMethod),["districts"]=typeof(District),["delivery-areas"]=typeof(DeliveryArea),["payment-gateways"]=typeof(PaymentGateway),["menu-groups"]=typeof(Menu),["role-menus"]=typeof(RoleMenuPermission),["user-menus"]=typeof(UserMenuPermission),["campaigns"]=typeof(Campaign),["banners"]=typeof(Banner),["attributes"]=typeof(ProductAttribute),["attribute-values"]=typeof(AttributeValue),["variant-attributes"]=typeof(VariantAttribute),["pages"]=typeof(ContentPage),["periods"]=typeof(AccountingPeriod),["companies"]=typeof(Company),["branches"]=typeof(Branch),["warehouses"]=typeof(Warehouse),["stores"]=typeof(Store),["currencies"]=typeof(Currency),["taxes"]=typeof(TaxRate),["fiscal-years"]=typeof(FiscalYear),["settings"]=typeof(BusinessSetting),["categories"]=typeof(Category),["brands"]=typeof(Brand),["units"]=typeof(Unit),["products"]=typeof(Product),["variants"]=typeof(ProductSku),["suppliers"]=typeof(Supplier),["coupons"]=typeof(Coupon),["delivery-zones"]=typeof(DeliveryZone),["delivery-agents"]=typeof(DeliveryAgent),["accounts"]=typeof(AccountHead),["expense-categories"]=typeof(ExpenseCategory),["menus"]=typeof(MenuItem),["permissions"]=typeof(PermissionGrant)};
 public static PropertyInfo[] Fields(Type t)=>t.GetProperties().Where(p=>p.CanWrite&&p.Name is not ("Id" or "CreatedAt" or "UpdatedAt" or "CreatedBy" or "UpdatedBy" or "Version")&&(p.PropertyType.IsValueType||p.PropertyType==typeof(string))).ToArray();
 public static string Permission(string module,string action)=>module switch {"payment-gateways" or "role-menus" or "user-menus" or "menu-groups" or "permissions" or "menus" or "companies" or "branches" or "currencies" or "taxes" or "fiscal-years" or "settings"=>"security.manage","warehouses" or "stores"=>"inventory."+(action=="view"?"view":"edit"),"suppliers"=>"purchase."+(action=="view"?"view":"edit"),"accounts" or "expense-categories" or "periods"=>"accounts."+(action=="view"?"view":"edit"),"delivery-methods" or "districts" or "delivery-areas" or "delivery-zones" or "delivery-agents"=>"delivery.edit",_=>"catalog."+action};
}
