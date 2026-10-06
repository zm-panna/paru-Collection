using System.Linq.Expressions;
using Ecommerce.Domain;
using Ecommerce.Application;
namespace Ecommerce.Web;
public class ReportFilters {
 public Guid? BranchId {get;set;} public Guid? WarehouseId {get;set;} public Guid? CategoryId {get;set;} public Guid? ProductId {get;set;} public Guid? SupplierId {get;set;}
 public string? CustomerId {get;set;} public string? OrderStatus {get;set;} public string? PaymentStatus {get;set;} public string? PaymentMethod {get;set;} public string? DeliveryStatus {get;set;}
 public IQueryable<Entity> Apply(IQueryable<Entity> query,Type type){
  foreach(var f in GetType().GetProperties()){var value=f.GetValue(this);if(value==null||value is string s&&string.IsNullOrWhiteSpace(s))continue;
   var path=Path(type,f.Name);
   if(path==null&&(f.Name=="ProductId"||f.Name=="CategoryId")&&Path(type,"OrderStatus") is string orderStatusPath){var rowParam=Expression.Parameter(typeof(Entity),"row");Expression order=Expression.Convert(rowParam,type);var prefix=orderStatusPath[..^6].TrimEnd('.');if(prefix.Length>0)foreach(var part in prefix.Split('.'))order=Expression.Property(order,part);var line=Expression.Parameter(typeof(OrderLine),"line");Expression key=Expression.Property(line,"Sku");if(f.Name=="CategoryId")key=Expression.Property(key,"Product");key=Expression.Property(key,f.Name);var predicate=Expression.Lambda<Func<OrderLine,bool>>(Expression.Equal(key,Expression.Constant(value)),line);var any=Expression.Call(typeof(Enumerable),"Any",new[]{typeof(OrderLine)},Expression.Property(order,"Lines"),predicate);query=query.Where(Expression.Lambda<Func<Entity,bool>>(any,rowParam));continue;}
   if(path==null)throw new BusinessException($"{f.Name} is not applicable to this report. Clear that filter.");
   var row=Expression.Parameter(typeof(Entity),"row");Expression member=Expression.Convert(row,type);foreach(var part in path.Split('.'))member=Expression.Property(member,part);
   object converted=value;var target=Nullable.GetUnderlyingType(member.Type)??member.Type;if(target.IsEnum){if(!Enum.TryParse(target,value.ToString(),true,out var parsed))throw new BusinessException("Invalid status filter.");converted=parsed!;}
   var right=Expression.Convert(Expression.Constant(converted),member.Type);query=query.Where(Expression.Lambda<Func<Entity,bool>>(Expression.Equal(member,right),row));
  }return query;
 }
 public static IQueryable<Entity> WithStatus(IQueryable<Entity> query,Type type,string status){var property=type.GetProperty("Status")??throw new BusinessException("Status does not apply to this report.");object value=status;if(property.PropertyType.IsEnum){if(!Enum.TryParse(property.PropertyType,status,true,out var parsed))throw new BusinessException("Invalid status.");value=parsed!;}var row=Expression.Parameter(typeof(Entity),"row");var member=Expression.Property(Expression.Convert(row,type),property);return query.Where(Expression.Lambda<Func<Entity,bool>>(Expression.Equal(member,Expression.Constant(value,property.PropertyType)),row));}
 static string? Path(Type t,string field){
  var order=t==typeof(Order)?"":t==typeof(OrderLine)||t==typeof(PaymentTransaction)||t==typeof(PaymentRefund)||t==typeof(ReturnRequest)||t==typeof(Delivery)?"Order.":t==typeof(CampaignUse)?"Order.":t==typeof(OrderAllocation)?"OrderLine.Order.":t==typeof(PaymentAttempt)?"PaymentTransaction.Order.":null;
  var purchase=t==typeof(PurchaseOrder)?"":t==typeof(PurchaseLine)||t==typeof(GoodsReceipt)?"PurchaseOrder.":t==typeof(SupplierInvoice)?"PurchaseOrder.":t==typeof(SupplierInvoiceLine)?"SupplierInvoice.PurchaseOrder.":t==typeof(SupplierReturn)?"PurchaseLine.PurchaseOrder.":null;
  var sku=t==typeof(ProductSku)?"":t==typeof(StockBalance)||t==typeof(StockMovement)||t==typeof(OrderLine)?"Sku.":t==typeof(ReturnRequest)||t==typeof(OrderAllocation)?"OrderLine.Sku.":null;
  var product=t==typeof(Product)?"":sku!=null?sku+"Product.":t==typeof(ProductView)?"Product.":null;
  if(field=="CustomerId")return order!=null?order+"UserId":t.GetProperty("UserId")!=null?"UserId":null;
  if(field is "OrderStatus" or "PaymentStatus" or "PaymentMethod")return order==null?null:order+(field=="OrderStatus"?"Status":field);
  if(field=="DeliveryStatus")return t==typeof(Delivery)?"Status":null;
  if(field=="ProductId")return t==typeof(Product)?"Id":sku!=null?sku+"ProductId":t.GetProperty("ProductId")!=null?"ProductId":null;
  if(field=="CategoryId")return product==null?null:product+"CategoryId";
  if(field=="SupplierId")return t==typeof(Supplier)?"Id":purchase!=null?purchase+"SupplierId":t.GetProperty("SupplierId")!=null?"SupplierId":null;
  if(field=="WarehouseId")return t.GetProperty(field)!=null?field:order!=null?order+field:purchase!=null?purchase+field:null;
  if(field=="BranchId")return t.GetProperty(field)!=null?field:t.GetProperty("WarehouseId")!=null?"Warehouse.BranchId":order!=null?order+"Warehouse.BranchId":purchase!=null?purchase+"Warehouse.BranchId":null;
  return null;
 }
}
