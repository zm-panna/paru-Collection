using System.Security.Claims;
using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
[Route("admin/security"),Permit("security.manage")]
public class SecurityController(UserManager<IdentityUser> users,RoleManager<IdentityRole> roles,AppDb db):Controller {
 [HttpGet("")]public async Task<IActionResult> Index(){ViewBag.Roles=await roles.Roles.Select(x=>x.Name!).ToListAsync();var all=await users.Users.Take(200).ToListAsync();var memberships=new Dictionary<string,string>();foreach(var u in all)memberships[u.Id]=string.Join(", ",await users.GetRolesAsync(u));ViewBag.Memberships=memberships;return View(all);}
 [HttpPost("role")]public async Task<IActionResult> Role(string name){if(string.IsNullOrWhiteSpace(name)||name.Length>60)return BadRequest();var r=await roles.CreateAsync(new IdentityRole(name));TempData["Message"]=r.Succeeded?"Role created.":string.Join(" ",r.Errors.Select(x=>x.Description));return RedirectToAction(nameof(Index));}
 [HttpPost("assign")]public async Task<IActionResult> Assign(string userId,string role,bool remove=false){if(role=="SuperAdmin")return BadRequest("SuperAdmin changes are restricted to controlled provisioning.");if(!await roles.RoleExistsAsync(role))return BadRequest();var u=await users.FindByIdAsync(userId);if(u==null)return NotFound();var result=remove?await users.RemoveFromRoleAsync(u,role):await users.AddToRoleAsync(u,role);if(result.Succeeded){await users.UpdateSecurityStampAsync(u);db.Add(new AuditEntry{ActorId=User.FindFirstValue(ClaimTypes.NameIdentifier)!,Action=remove?"RoleRemoved":"RoleAssigned",EntityType="IdentityUser",EntityId=u.Id,After=role});await db.SaveChangesAsync();}TempData["Message"]=result.Succeeded?"Role updated. User should sign in again.":string.Join(" ",result.Errors.Select(x=>x.Description));return RedirectToAction(nameof(Index));}
}
