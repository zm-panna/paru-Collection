using System.Security.Claims;
using Ecommerce.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure;

public class AccessService(AppDb db)
{
    private static bool FullAdmin(ClaimsPrincipal user)
    {
        // Identity role comparison is normally case-insensitive, but keep the
        // accepted operational/provisioning role names explicit.
        return user.IsInRole("Admin") || user.IsInRole("SuperAdmin");
    }

    public async Task<bool> HasAsync(ClaimsPrincipal user, string permission)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        // Admin/SuperAdmin always have full application access.
        if (FullAdmin(user))
            return true;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        // User-specific permission has priority.
        var userPermission = await db.Set<PermissionGrant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.Permission == permission);

        if (userPermission != null)
            return userPermission.Allowed;

        var roleNames = user.FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .Distinct()
            .ToArray();

        if (roleNames.Length == 0)
            return false;

        return await db.Set<PermissionGrant>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == "" &&
                roleNames.Contains(x.Role) &&
                x.Permission == permission &&
                x.Allowed);
    }

    public async Task<List<MenuItem>> MenuAsync(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return new List<MenuItem>();

        // Read navigation directly from Menu/MenuItem.
        // Only active parent menu groups are returned.
        var allMenuItems = await db.Set<MenuItem>()
            .AsNoTracking()
            .Include(x => x.Menu)
            .Where(x => x.Menu == null || x.Menu.Active)
            .OrderBy(x => x.Menu == null ? int.MinValue : x.Menu.SortOrder)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync();

        // IMPORTANT:
        // Admin and SuperAdmin do not require PermissionGrant,
        // RoleMenuPermission or UserMenuPermission rows.
        // Every DB MenuItem under an active Menu is visible.
        if (FullAdmin(user))
            return allMenuItems;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        var roleNames = user.FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .Distinct()
            .ToArray();

        var userRules = await db.Set<UserMenuPermission>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var roleRules = await db.Set<RoleMenuPermission>()
            .AsNoTracking()
            .Where(x => roleNames.Contains(x.Role))
            .ToListAsync();

        var result = new List<MenuItem>();

        foreach (var menuItem in allMenuItems)
        {
            // Normal users must have the permission attached to the MenuItem.
            if (!await HasAsync(user, menuItem.Permission))
                continue;

            // Explicit user rule overrides role rule.
            var userRule = userRules
                .FirstOrDefault(x => x.MenuItemId == menuItem.Id);

            if (userRule != null)
            {
                if (userRule.Allowed)
                    result.Add(menuItem);

                continue;
            }

            var matchingRoleRules = roleRules
                .Where(x => x.MenuItemId == menuItem.Id)
                .ToList();

            // No role-menu rule means permission is enough.
            // If rules exist, at least one role must allow the item.
            if (matchingRoleRules.Count == 0 ||
                matchingRoleRules.Any(x => x.Allowed))
            {
                result.Add(menuItem);
            }
        }

        return result;
    }
}
