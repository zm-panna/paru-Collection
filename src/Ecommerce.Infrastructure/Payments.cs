using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data;
using Ecommerce.Domain;
using Ecommerce.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace Ecommerce.Infrastructure;
public class SslCommerzGateway(HttpClient http,IConfiguration config):IPaymentGateway {
 public string Name=>"SSLCommerz";
 string Host=>config.GetValue<bool>("Payments:SSLCommerz:Sandbox")?"https://sandbox.sslcommerz.com":"https://securepay.sslcommerz.com";
 string Setting(string key)=>config["Payments:SSLCommerz:"+key] is {Length:>0} s?s:throw new BusinessException("SSLCommerz credentials are not configured.");
 public async Task<GatewaySession> CreateAsync(Order o,PaymentTransaction p,CancellationToken ct){
  var root=config["PublicBaseUrl"]?.TrimEnd('/')??throw new BusinessException("PublicBaseUrl is required.");
  if(!Uri.TryCreate(root,UriKind.Absolute,out var url)||url.Scheme!="https")throw new BusinessException("Payment callbacks need a public HTTPS base URL.");
  var fields=new Dictionary<string,string>{{"store_id",Setting("StoreId")},{"store_passwd",Setting("StorePassword")},{"total_amount",p.Amount.ToString("0.00",CultureInfo.InvariantCulture)},{"currency","BDT"},{"tran_id",p.TransactionId},{"success_url",root+"/payment/callback"},{"fail_url",root+"/payment/result"},{"cancel_url",root+"/payment/result"},{"ipn_url",root+"/payment/callback"},{"cus_name",o.CustomerName},{"cus_email",o.Email},{"cus_add1",o.ShippingAddress},{"cus_city","Dhaka"},{"cus_country","Bangladesh"},{"cus_phone",o.Phone},{"shipping_method","NO"},{"product_name",o.Number},{"product_category","Retail"},{"product_profile","general"}};
  using var response=await http.PostAsync(Host+"/gwprocess/v4/api.php",new FormUrlEncodedContent(fields),ct);response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
  if(doc.RootElement.GetProperty("status").GetString()!="SUCCESS")throw new BusinessException("Gateway could not create a payment session.");
  var redirect=doc.RootElement.GetProperty("GatewayPageURL").GetString()??"";
  if(!Uri.TryCreate(redirect,UriKind.Absolute,out var dest)||dest.Scheme!="https"||!(dest.Host=="sslcommerz.com"||dest.Host.EndsWith(".sslcommerz.com",StringComparison.OrdinalIgnoreCase)))throw new BusinessException("Unexpected gateway redirect.");
  return new GatewaySession(redirect,p.TransactionId);
 }
 public async Task<VerifiedPayment> VerifyAsync(string validationId,CancellationToken ct){
  if(string.IsNullOrWhiteSpace(validationId)||validationId.Length>200)throw new BusinessException("Invalid payment validation ID.");
  var path=Host+"/validator/api/validationserverAPI.php?val_id="+Uri.EscapeDataString(validationId)+"&store_id="+Uri.EscapeDataString(Setting("StoreId"))+"&store_passwd="+Uri.EscapeDataString(Setting("StorePassword"))+"&format=json";
  using var response=await http.GetAsync(path,ct);response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));var e=doc.RootElement;
  string Get(string n)=>e.TryGetProperty(n,out var v)?v.ToString():"";
  decimal.TryParse(Get("amount"),NumberStyles.Number,CultureInfo.InvariantCulture,out var amount);
  return new VerifiedPayment(Get("status") is "VALID" or "VALIDATED",Get("tran_id"),Get("bank_tran_id"),amount,Get("currency"),Get("risk_level"));
 }
}
public class CodGateway:IPaymentGateway {
 public string Name=>"COD";
 public Task<GatewaySession> CreateAsync(Order o,PaymentTransaction p,CancellationToken ct)=>Task.FromResult(new GatewaySession("/orders/"+o.Id,p.TransactionId));
 public Task<VerifiedPayment> VerifyAsync(string validationId,CancellationToken ct)=>Task.FromResult(new VerifiedPayment(false,"","",0,"BDT",""));
}
public class PaymentGatewayFactory(IEnumerable<IPaymentGateway> gateways){public IPaymentGateway Get(string name)=>gateways.SingleOrDefault(x=>x.Name==name)??throw new BusinessException("This payment provider is not enabled.");}
public class PaymentService(AppDb db,PaymentGatewayFactory gateways,LedgerService ledger){
 public async Task<GatewaySession> StartAsync(Guid orderId,string user,CancellationToken ct){
  var order=await db.Set<Order>().SingleOrDefaultAsync(x=>x.Id==orderId&&x.UserId==user)??throw new BusinessException("Order not found.");
  if(order.Status==OrderStatus.Cancelled||order.PaymentStatus=="Paid"||order.PaymentMethod!="SSLCommerz")throw new BusinessException("Payment cannot be started for this order.");
  // Reuse one merchant transaction per order, preventing multiple independently payable attempts.
  await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable)){
   if(!await db.Set<PaymentTransaction>().AnyAsync(x=>x.OrderId==order.Id&&x.Gateway=="SSLCommerz"))db.Add(new PaymentTransaction{OrderId=order.Id,TransactionId="PAY-"+order.Id.ToString("N")[..26],Gateway="SSLCommerz",Amount=order.Total});
   await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
  }
  var p=await db.Set<PaymentTransaction>().SingleAsync(x=>x.OrderId==orderId&&x.Gateway=="SSLCommerz");
  var result=await gateways.Get("SSLCommerz").CreateAsync(order,p,ct);db.Add(new PaymentAttempt{PaymentTransactionId=p.Id,Status="SessionCreated",Message="Hosted checkout session requested"});await db.SaveChangesAsync(ct);return result;
 }
 public async Task VerifyAsync(string validationId,CancellationToken ct){
  var v=await gateways.Get("SSLCommerz").VerifyAsync(validationId,ct);
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  var p=await db.Set<PaymentTransaction>().Include(x=>x.Order).SingleOrDefaultAsync(x=>x.TransactionId==v.TransactionId,ct)??throw new BusinessException("Unknown transaction.");
  if(!CommerceRules.AcceptPayment(p,v)||string.IsNullOrWhiteSpace(v.GatewayId))throw new BusinessException("Payment verification failed or risk review is required.");
  if(p.Status=="Paid")return;
  if(p.Order.Status==OrderStatus.Cancelled)throw new BusinessException("Payment received for a cancelled order; contact support for reconciliation.");
  if(await db.Set<PaymentTransaction>().AnyAsync(x=>x.GatewayTransactionId==v.GatewayId&&x.Id!=p.Id,ct))throw new BusinessException("Gateway transaction has already been used.");
  p.Status="Paid";p.VerificationStatus="Verified";p.GatewayTransactionId=v.GatewayId;p.VerifiedAt=DateTime.UtcNow;p.Order.PaymentStatus="Paid";
  db.Add(new PaymentCallbackLog{TransactionId=p.TransactionId,ValidationId=validationId,Status="Verified"});
  await ledger.PostAsync("PAY-"+p.Order.Number,"Verified gateway payment",("1010",p.Amount,0,""),("1100",0,p.Amount,p.Order.UserId));
  ledger.Audit("SSLCommerz","PaymentVerified",p);ledger.Notify(p.Order.UserId,"Payment verified for "+p.Order.Number);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
 }
}
