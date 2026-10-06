using Ecommerce.Application;
using Ecommerce.Domain;
using Xunit;
namespace Ecommerce.UnitTests;
public class CommerceRuleTests {
 [Theory][InlineData(0)][InlineData(-1)][InlineData(10001)]public void RejectsInvalidQuantities(int q)=>Assert.Throws<BusinessException>(()=>CommerceRules.Quantity(q));
 [Theory][InlineData(OrderStatus.Pending,OrderStatus.Delivered)][InlineData(OrderStatus.Shipped,OrderStatus.Cancelled)][InlineData(OrderStatus.Delivered,OrderStatus.Confirmed)]public void RejectsInvalidOrderTransitions(OrderStatus from,OrderStatus to)=>Assert.False(CommerceRules.CanTransition(from,to));
 [Fact]public void ValidShippingTransition()=>Assert.True(CommerceRules.CanTransition(OrderStatus.ReadyToShip,OrderStatus.Shipped));
 [Fact]public void CouponCapsDiscount(){var c=new Coupon{Percent=50,MaximumDiscount=100,MinimumPurchase=50,UsageLimit=10,PerCustomerLimit=1,StartsAt=DateTime.UtcNow.AddDays(-1),EndsAt=DateTime.UtcNow.AddDays(1)};Assert.Equal(100m,CommerceRules.Discount(c,500,0,0,DateTime.UtcNow));}
 [Fact]public void CouponRejectsReuse(){var c=new Coupon{UsageLimit=10,PerCustomerLimit=1,StartsAt=DateTime.UtcNow.AddDays(-1),EndsAt=DateTime.UtcNow.AddDays(1)};Assert.Throws<BusinessException>(()=>CommerceRules.Discount(c,500,1,1,DateTime.UtcNow));}
 [Fact]public void JournalRejectsImbalance()=>Assert.Throws<BusinessException>(()=>CommerceRules.Balanced(new[]{(100m,0m),(0m,90m)}));
 [Fact]public void JournalRejectsNegativeEntries()=>Assert.Throws<BusinessException>(()=>CommerceRules.Balanced(new[]{(-10m,0m),(0m,-10m)}));
 [Fact]public void RejectsSubCentJournal()=>Assert.Throws<BusinessException>(()=>CommerceRules.Balanced(new[]{(0.004m,0m),(0m,0.004m)}));
 [Fact]public void JournalAcceptsBalancedEntries()=>CommerceRules.Balanced(new[]{(100m,0m),(0m,100m)});
 [Theory][InlineData(99,"BDT","0","trx")][InlineData(100,"USD","0","trx")][InlineData(100,"BDT","1","trx")][InlineData(100,"BDT","0","other")]
 public void RejectsPaymentMismatch(decimal amount,string currency,string risk,string transaction){var p=new PaymentTransaction{Amount=100,Currency="BDT",TransactionId="trx"};Assert.False(CommerceRules.AcceptPayment(p,new VerifiedPayment(true,transaction,"bank",amount,currency,risk)));}
 [Fact]public void AcceptsExactVerifiedPayment(){var p=new PaymentTransaction{Amount=100,TransactionId="trx"};Assert.True(CommerceRules.AcceptPayment(p,new VerifiedPayment(true,"trx","bank",100,"BDT","0")));}
 [Fact]public void BrowserSuccessAloneCannotVerifyPayment(){var p=new PaymentTransaction{Amount=100,TransactionId="trx"};Assert.False(CommerceRules.AcceptPayment(p,new VerifiedPayment(false,"trx","bank",100,"BDT","0")));}
}
