using Ecommerce.Infrastructure;
using Xunit;
namespace Ecommerce.IntegrationTests;
public class BrandingSecurityTests
{
 [Theory]
 [InlineData("javascript:alert(1)")]
 [InlineData("//untrusted.example/logo.png")]
 [InlineData("/\\untrusted.example/logo.png")]
 public void BrandingRejectsUnsafeAssetUrls(string url)=>Assert.Equal("",BrandingService.SafeUrl(url));
 [Theory]
 [InlineData("https://m.me/shop","https://m.me/shop")]
 [InlineData("https://m.me.evil.example/shop","")]
 [InlineData("http://m.me/shop","")]
 public void MessengerRequiresApprovedHttpsHost(string url,string expected)=>Assert.Equal(expected,BrandingService.SafeMessenger(url));
 [Fact]public void ThemeRejectsInjectedCss()=>Assert.Equal("#153f35",BrandingService.Color("red; background:url(https://evil.example)","#153f35"));
}
