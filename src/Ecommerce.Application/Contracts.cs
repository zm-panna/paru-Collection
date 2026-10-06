using Ecommerce.Domain;
namespace Ecommerce.Application;
public class BusinessException(string message):Exception(message);
public interface IRepository<T> where T:Entity {IQueryable<T> Query();Task<T?> FindAsync(Guid id);Task AddAsync(T entity);Task<int> SaveAsync();}
public record CheckoutRequest(string UserId,string Name,string Phone,string Email,string Address,Guid ZoneId,Guid WarehouseId,string Coupon,string Method,string Key,string Source="Online",string? CartOwner=null);
public record GatewaySession(string Url,string TransactionId);
public record VerifiedPayment(bool Valid,string TransactionId,string GatewayId,decimal Amount,string Currency,string RiskLevel);
public interface IPaymentGateway {string Name {get;}Task<GatewaySession> CreateAsync(Order order,PaymentTransaction payment,CancellationToken ct);Task<VerifiedPayment> VerifyAsync(string validationId,CancellationToken ct);}
public interface IAIService {Task<string> AskAsync(string question,string userId,bool admin,CancellationToken ct);}
public interface ICourierProvider {Task<string> BookAsync(Order order,CancellationToken ct);}
public interface INotificationService {Task NotifyAsync(string userId,string message);}
public static class CommerceRules {
 public static decimal Money(decimal n)=>Math.Round(n,2,MidpointRounding.AwayFromZero);
 public static decimal Discount(Coupon c,decimal subtotal,int totalUses,int customerUses,DateTime now) {
  if(!c.Active||now<c.StartsAt||now>c.EndsAt||subtotal<c.MinimumPurchase||totalUses>=c.UsageLimit||customerUses>=c.PerCustomerLimit)throw new BusinessException("Coupon is unavailable or its limit has been reached.");
  return Money(Math.Min(subtotal,Math.Min(c.MaximumDiscount,subtotal*c.Percent/100m)));
 }
 public static void Quantity(int q){if(q<1||q>10000)throw new BusinessException("Quantity must be between 1 and 10,000.");}
 public static bool CanTransition(OrderStatus from,OrderStatus to)=> (from,to) switch {
  (OrderStatus.Pending,OrderStatus.Confirmed) or (OrderStatus.Confirmed,OrderStatus.Processing) or (OrderStatus.Processing,OrderStatus.Packed) or (OrderStatus.Packed,OrderStatus.ReadyToShip) or (OrderStatus.ReadyToShip,OrderStatus.Shipped) or (OrderStatus.Shipped,OrderStatus.OutForDelivery) or (OrderStatus.OutForDelivery,OrderStatus.Delivered)=>true,
  (_,OrderStatus.Cancelled)=>from is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Processing or OrderStatus.Packed or OrderStatus.ReadyToShip,
  _=>false
 };
 public static void Balanced(IEnumerable<(decimal Debit,decimal Credit)> lines){var a=lines.ToList();if(a.Count<2||a.Any(x=>x.Debit!=Money(x.Debit)||x.Credit!=Money(x.Credit)||x.Debit<0||x.Credit<0||(x.Debit>0&&x.Credit>0))||a.Sum(x=>x.Debit)<=0||a.Sum(x=>x.Debit)!=a.Sum(x=>x.Credit))throw new BusinessException("Journal must have equal nonzero debit and credit totals.");}
 public static bool AcceptPayment(PaymentTransaction p,VerifiedPayment v)=>v.Valid&&v.TransactionId==p.TransactionId&&v.Amount==p.Amount&&v.Currency==p.Currency&&v.RiskLevel=="0";
}
