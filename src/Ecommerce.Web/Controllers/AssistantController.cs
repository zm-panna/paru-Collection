using System.Security.Claims;
using Ecommerce.Application;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Ecommerce.Web.Controllers;
[Authorize,Route("assistant"),EnableRateLimiting("ai")]
public class AssistantController(IAIService ai,AccessService access):Controller {
 [HttpGet("")]public IActionResult Index()=>View();
 [HttpPost("ask")]public async Task<IActionResult> Ask(string question,CancellationToken ct){var admin=await access.HasAsync(User,"ai.admin")&&await access.HasAsync(User,"reports.view")&&await access.HasAsync(User,"inventory.view");var reply=await ai.AskAsync(question,User.FindFirstValue(ClaimTypes.NameIdentifier)!,admin,ct);return Json(new{reply});}
}
