using System.ComponentModel.DataAnnotations;
namespace Ecommerce.Domain;
public abstract class Entity {
 public Guid Id {get;set;}=Guid.NewGuid();
 public string CreatedBy {get;set;}="system";
 public string UpdatedBy {get;set;}="";
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
 public DateTime? UpdatedAt {get;set;}
 [Timestamp] public byte[] Version {get;set;}=[];
}
public abstract class NamedEntity : Entity
{
    [Required]
    [StringLength(860)]
    public string Name { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public override string ToString()
    {
        return Name;
    }
}

public class Company:NamedEntity { public string Address {get;set;}=""; public string Phone {get;set;}=""; public string TaxNumber {get;set;}=""; }
public class Branch:NamedEntity {public Guid CompanyId {get;set;} public Company Company {get;set;}=null!; public string Address {get;set;}="";}
public class Warehouse:NamedEntity {public Guid BranchId {get;set;} public Branch Branch {get;set;}=null!;}
public class Store:NamedEntity {public Guid WarehouseId {get;set;} public Warehouse Warehouse {get;set;}=null!;}
public class Currency:NamedEntity { [Required,StringLength(3)] public string Code {get;set;}="BDT"; public decimal ExchangeRate {get;set;}=1;}
public class TaxRate:NamedEntity {[Range(0,100)] public decimal Percent {get;set;}}
public class FiscalYear:NamedEntity {public DateTime StartDate {get;set;} public DateTime EndDate {get;set;}}
public class BusinessSetting:NamedEntity {public string Value {get;set;}=""; public string Group {get;set;}="Business";}

public class Category:NamedEntity {public string MetaKeywords {get;set;}="";public string BannerPath {get;set;}="";public Guid? ParentId {get;set;} public Category? Parent {get;set;} [Required] public string Slug {get;set;}=""; public string Description {get;set;}="";public int SortOrder {get;set;} public string ImagePath {get;set;}="/images/fallback.svg";public string MetaTitle {get;set;}="";public string MetaDescription {get;set;}="";}

public class Brand : NamedEntity
{
    public string Slug { get; set; } = "";

    public string? Description { get; set; }

    public string? LogoPath { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public string? MetaKeywords { get; set; }
}

public class Unit:NamedEntity {public string Symbol {get;set;}="pcs";}
public class Product:NamedEntity {
 [Required] public string Code {get;set;}="";[Required] public string Slug {get;set;}="";
 public Guid CategoryId {get;set;} public Category Category {get;set;}=null!;
 public Guid? BrandId {get;set;} public Brand? Brand {get;set;} public Guid? UnitId {get;set;} public Unit? Unit {get;set;}
 public string Description {get;set;}="";public string ShortName {get;set;}="";
 public string Warranty {get;set;}="";public string ReturnPolicy {get;set;}="7 days subject to inspection";
 public decimal Weight {get;set;} public string Dimensions {get;set;}="";
 public string MetaTitle {get;set;}="";public string MetaDescription {get;set;}="";public string MetaKeywords {get;set;}="";
 public bool Featured {get;set;} public bool NewArrival {get;set;} public bool BestSeller {get;set;}
 public List<ProductSku> Skus {get;set;}=[];public List<ProductFile> Files {get;set;}=[];
}
public class ProductSku:NamedEntity {
 public Guid ProductId {get;set;} public Product Product {get;set;}=null!;
 [Required] public string SKU {get;set;}="";public string? Barcode {get;set;}=null;
 public string? Color {get;set;}="";public string? Size {get;set;}="";public string? Attributes {get;set;}="";
 [Range(0,100000000)] public decimal PurchasePrice {get;set;}
 [Range(0,100000000)] public decimal RegularPrice {get;set;}
 [Range(0,100000000)] public decimal SalePrice {get;set;}
 [Range(0,100)] public decimal VatPercent {get;set;}
 [Range(0,1000000)] public int ReorderLevel {get;set;}=5;
}
public class ProductFile:Entity {public Guid ProductId {get;set;}public Product Product {get;set;}=null!;public string ThumbnailPath {get;set;}="";public string? FileName {get;set;}="";public string? FilePath {get;set;}="";public string? FileType {get;set;}="";public int SortOrder {get;set;} public bool IsPrimary {get;set;}}
public class Supplier:NamedEntity {public string Phone {get;set;}="";public string Email {get;set;}="";public string Address {get;set;}="";public string TaxNumber {get;set;}="";}
public class Coupon:NamedEntity {[Required] public string Code {get;set;}="";[Range(0,100)]public decimal Percent {get;set;} public decimal MaximumDiscount {get;set;}=1000;public decimal MinimumPurchase {get;set;} public DateTime StartsAt {get;set;}=DateTime.UtcNow;public DateTime EndsAt {get;set;}=DateTime.UtcNow.AddMonths(1); public int UsageLimit {get;set;}=100;public int PerCustomerLimit {get;set;}=1;}
public class CouponUse:Entity {public Guid CouponId {get;set;}public Coupon Coupon {get;set;}=null!;public string UserId {get;set;}="";public Guid OrderId {get;set;} public Order Order {get;set;}=null!;}
public class DeliveryZone : NamedEntity
{
    // Delivery Method
    public Guid? DeliveryMethodId { get; set; }
    public DeliveryMethod? DeliveryMethod { get; set; }

    // Delivery Area
    public Guid? DeliveryAreaId { get; set; }
    public DeliveryArea? DeliveryArea { get; set; }

    // Location
    public string District { get; set; } = "";
    public string Area { get; set; } = "";

    // Delivery Charge
    public decimal Charge { get; set; } = 60;

    // Free delivery threshold
    public decimal? FreeAbove { get; set; }
}

public class DeliveryAgent:NamedEntity {public string Phone {get;set;}="";public string Courier {get;set;}="Own team";}
public class Address:Entity {public string UserId {get;set;}="";[Required]public string Recipient {get;set;}="";[Required]public string Phone {get;set;}="";[Required]public string Street {get;set;}="";public string District {get;set;}="";public string Area {get;set;}="";public bool IsDefault {get;set;}}
public class WishlistItem:Entity {public string UserId {get;set;}="";public Guid ProductId {get;set;}public Product Product {get;set;}=null!;}
public class CartItem:Entity {public string Owner {get;set;}="";public Guid SkuId {get;set;}public ProductSku Sku {get;set;}=null!;public int Quantity { get; set; }public string Phone { get;set;}="";}
public class StockBalance:Entity {public Guid WarehouseId {get;set;}public Warehouse Warehouse {get;set;}=null!;public Guid SkuId {get;set;}public ProductSku Sku {get;set;}=null!;public int OnHand {get;set;}public int Reserved {get;set;}public decimal AverageCost {get;set;}}
public class StockMovement:Entity {public Guid WarehouseId {get;set;}public Warehouse Warehouse {get;set;}=null!;public Guid SkuId {get;set;}public ProductSku Sku {get;set;}=null!;public int Quantity {get;set;}public int ReservedChange {get;set;}public decimal UnitCost {get;set;}public string Kind {get;set;}="";public string Reference {get;set;}="";public string ActorId {get;set;}="";}
public enum OrderStatus {Pending,Confirmed,Processing,Packed,ReadyToShip,Shipped,OutForDelivery,Delivered,Cancelled,Returned,Refunded}
public class Order : Entity
{
    public string Number { get; set; } = "";
    public string UserId { get; set; } = "";

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    // Delivery
    public Guid? DeliveryAreaId { get; set; }
    public DeliveryArea? DeliveryArea { get; set; }

    public Guid? DeliveryMethodId { get; set; }
    public DeliveryMethod? DeliveryMethod { get; set; }

    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string ShippingAddress { get; set; } = "";

    public string Source { get; set; } = "Online";
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public string PaymentMethod { get; set; } = "COD";
    public string PaymentStatus { get; set; } = "Unpaid";

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }

    public string CheckoutKey { get; set; } = "";

    public List<OrderLine> Lines { get; set; } = [];
    public List<OrderHistory> History { get; set; } = [];
}

public class OrderLine:Entity {public Guid OrderId {get;set;}public Order Order {get;set;}=null!;public Guid SkuId {get;set;}public ProductSku Sku {get;set;}=null!;public string Name {get;set;}="";public int Quantity {get;set;}public decimal Price {get;set;}public decimal Cost {get;set;}public decimal Tax {get;set;}}
public class OrderHistory:Entity {public Guid OrderId {get;set;}public Order Order {get;set;}=null!;public string Status {get;set;}="";public string Note {get;set;}="";public string ActorId {get;set;}="";}
public class PaymentTransaction:Entity {public Guid OrderId {get;set;}public Order Order {get;set;}=null!;public string TransactionId {get;set;}="";public string Gateway {get;set;}="";public decimal Amount {get;set;}public string Currency {get;set;}="BDT";public string Status {get;set;}="Pending";public string GatewayTransactionId {get;set;}="";public string VerificationStatus {get;set;}="Unverified";public DateTime? VerifiedAt {get;set;}}
public class PaymentAttempt:Entity {public Guid PaymentTransactionId {get;set;}public PaymentTransaction PaymentTransaction {get;set;}=null!;public string Status {get;set;}="";public string Message {get;set;}="";}
public class PaymentCallbackLog:Entity {public string TransactionId {get;set;}="";public string Status {get;set;}="";public string ValidationId {get;set;}="";}
public class PaymentRefund:Entity {public Guid OrderId {get;set;}public Order Order {get;set;}=null!;public decimal Amount {get;set;}public string Status {get;set;}="Requested";public string ExternalReference {get;set;}="";public string Reason {get;set;}="";}
public class Delivery:Entity {public Guid OrderId {get;set;}public Order Order {get;set;}=null!;public Guid? DeliveryAgentId {get;set;}public DeliveryAgent? DeliveryAgent {get;set;}public string TrackingNumber {get;set;}="";public string Status {get;set;}="Assigned";}
public class PurchaseOrder:Entity {public string Number {get;set;}="";public Guid SupplierId {get;set;}public Supplier Supplier {get;set;}=null!;public Guid WarehouseId {get;set;}public Warehouse Warehouse {get;set;}=null!;public string Status {get;set;}="Draft";public decimal Total {get;set;}public decimal Paid {get;set;}public decimal ReceivedTotal {get;set;}public decimal ReturnedTotal {get;set;}public List<PurchaseLine> Lines {get;set;}=[];}
public class PurchaseLine:Entity {public Guid PurchaseOrderId {get;set;}public PurchaseOrder PurchaseOrder {get;set;}=null!;public Guid SkuId {get;set;}public ProductSku Sku {get;set;}=null!;public int Quantity {get;set;}public decimal UnitCost {get;set;}public int ReceivedQuantity {get;set;}public int ReturnedQuantity {get;set;}}
public class ReturnRequest:Entity {public Guid OrderId {get;set;}public Order Order {get;set;}=null!;public Guid OrderLineId {get;set;}public OrderLine OrderLine {get;set;}=null!;public int Quantity {get;set;}public string Reason {get;set;}="";public string Status {get;set;}="Requested";public bool Restock {get;set;}public string Inspection {get;set;}="";}
public class Review:Entity {public string UserId {get;set;}="";public Guid ProductId {get;set;}public Product Product {get;set;}=null!;[Range(1,5)]public int Rating {get;set;}public string Comment {get;set;}="";public bool Approved {get;set;}public bool VerifiedPurchase {get;set;}}
public class SupportTicket:Entity {public string UserId {get;set;}="";public string Subject {get;set;}="";public string Message {get;set;}="";public string Status {get;set;}="Open";public string AssignedTo {get;set;}="";public string Response {get;set;}="";}
public class AccountHead:NamedEntity {public string Code {get;set;}="";public string Type {get;set;}="Asset";}
public class Journal:Entity {public string Number {get;set;}="";public string Reference {get;set;}="";public string Description {get;set;}="";public DateTime Date {get;set;}=DateTime.UtcNow;public List<JournalLine> Lines {get;set;}=[];}
public class JournalLine:Entity {public Guid JournalId {get;set;}public Journal Journal {get;set;}=null!;public Guid AccountHeadId {get;set;}public AccountHead AccountHead {get;set;}=null!;public decimal Debit {get;set;}public decimal Credit {get;set;}public string Party {get;set;}="";}
public class ExpenseCategory:NamedEntity {}
public class Expense:Entity {public string PaymentMethod {get;set;}="Cash";public string AttachmentPath {get;set;}="";public Guid ExpenseCategoryId {get;set;}public ExpenseCategory ExpenseCategory {get;set;}=null!;public Guid BranchId {get;set;}public Branch Branch {get;set;}=null!;public string Description {get;set;}="";public decimal Amount {get;set;}public string Status {get;set;}="Draft";}
public class MenuItem:Entity {public Guid? MenuId {get;set;}public Menu? Menu {get;set;}public string Label {get;set;}="";public string Url {get;set;}="";public string Permission {get;set;}="";public string? Icon {get;set;}public int SortOrder {get;set;}}
public class PermissionGrant:Entity {public string Role {get;set;}="";public string UserId {get;set;}="";public string Permission {get;set;}="";public bool Allowed {get;set;}=true;}
public class AuditEntry:Entity {public string ActorId {get;set;}="";public string Action {get;set;}="";public string EntityType {get;set;}="";public string EntityId {get;set;}="";public string Before {get;set;}="";public string After {get;set;}="";}
public class Notification:Entity {public string UserId {get;set;}="";public string Message {get;set;}="";public bool Read {get;set;}}
public class SearchEvent:Entity {public string UserId {get;set;}="";public string Query {get;set;}="";}
