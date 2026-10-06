using Ecommerce.Application;
using Ecommerce.Domain;
namespace Ecommerce.Infrastructure;
// These adapters deliberately fail closed. They are not registered as checkout options.
// Implement merchant-specific contract authentication/create/query/refund methods before enabling.
public abstract class ContractRequiredGateway:IPaymentGateway {
 public abstract string Name {get;}
 public Task<GatewaySession> CreateAsync(Order order,PaymentTransaction payment,CancellationToken ct)=>Task.FromException<GatewaySession>(new BusinessException(Name+" direct API is not configured. Use the verified SSLCommerz hosted checkout when available."));
 public Task<VerifiedPayment> VerifyAsync(string validationId,CancellationToken ct)=>Task.FromResult(new VerifiedPayment(false,"","",0,"BDT",""));
}
public sealed class BkashGateway:ContractRequiredGateway {public override string Name=>"bKash";}
public sealed class NagadGateway:ContractRequiredGateway {public override string Name=>"Nagad";}
public sealed class BankGateway:ContractRequiredGateway {public override string Name=>"Bank";}
