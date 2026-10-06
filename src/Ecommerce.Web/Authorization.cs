using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Ecommerce.Infrastructure;
namespace Ecommerce.Web;
public record PermissionRequirement(string Permission):IAuthorizationRequirement;
public class PermissionHandler(AccessService access):AuthorizationHandler<PermissionRequirement>{protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,PermissionRequirement requirement){if(await access.HasAsync(context.User,requirement.Permission))context.Succeed(requirement);}}
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options):DefaultAuthorizationPolicyProvider(options){public override Task<AuthorizationPolicy?> GetPolicyAsync(string name)=>name.StartsWith("perm:")?Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(name[5..])).Build()):base.GetPolicyAsync(name);}
public class PermitAttribute(string permission):AuthorizeAttribute("perm:"+permission);
