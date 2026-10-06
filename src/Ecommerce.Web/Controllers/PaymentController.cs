using System.Security.Claims;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Ecommerce.Web.Controllers;
[Route("payment")]
public class PaymentController(PaymentService payment):Controller {
 [Authorize,HttpPost("start")]public async Task<IActionResult> Start(Guid id,CancellationToken ct){var s=await payment.StartAsync(id,User.FindFirstValue(ClaimTypes.NameIdentifier)!,ct);return Redirect(s.Url);}
 [AllowAnonymous,IgnoreAntiforgeryToken,HttpPost("callback")]
 public async Task<IActionResult> Callback([FromForm(Name="val_id")]string validationId,CancellationToken ct){await payment.VerifyAsync(validationId,ct);return Content("Payment verified. Open your order history to view the updated status.");}
 [AllowAnonymous,IgnoreAntiforgeryToken,AcceptVerbs("GET","POST"),Route("result")]public IActionResult Result()=>View();
}
