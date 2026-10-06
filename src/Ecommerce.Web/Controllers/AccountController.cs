using System.Security.Claims;
using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Web.Controllers;

[Route("account")]
[EnableRateLimiting("account")]
public class AccountController(
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    RoleManager<IdentityRole> roles,
    CartService carts,
    AccessService access,
    AppDb db) : Controller
{
    private const string CustomerRole = "CUSTOMER";
    private const string StaffRole = "STAFF";
    private const string AdminRole = "ADMIN";

    // ============================================================
    // LOGIN
    // CUSTOMER / STAFF / ADMIN
    // ============================================================

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(bool admin = false)
    {
        return View(new LoginVm
        {
            Admin = admin
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var loginValue = model.Email?.Trim();

        if (string.IsNullOrWhiteSpace(loginValue))
        {
            ModelState.AddModelError("", "Email or phone is required.");
            return View(model);
        }

        // --------------------------------------------------------
        // Find user by Email first, then Phone
        // --------------------------------------------------------

        var user =
            await users.FindByEmailAsync(loginValue)
            ?? await users.Users
                .SingleOrDefaultAsync(x =>
                    x.PhoneNumber == loginValue);

        if (user == null)
        {
            ModelState.AddModelError(
                "",
                "Invalid credentials or account temporarily locked.");

            return View(model);
        }

        // --------------------------------------------------------
        // Password verification
        // --------------------------------------------------------

        var result =
            await signIn.CheckPasswordSignInAsync(
                user,
                model.Password,
                lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                "",
                "Invalid credentials or account temporarily locked.");

            return View(model);
        }

        // --------------------------------------------------------
        // ADMIN login requested
        // Keep your existing permission system.
        // --------------------------------------------------------

        var principal =
            await signIn.CreateUserPrincipalAsync(user);

        if (model.Admin)
        {
            var canOpenDashboard =
                await access.HasAsync(
                    principal,
                    "dashboard.view");

            if (!canOpenDashboard)
            {
                ModelState.AddModelError(
                    "",
                    "This account cannot open the admin dashboard.");

                return View(model);
            }
        }

        // --------------------------------------------------------
        // Sign in
        // --------------------------------------------------------

        await signIn.SignInAsync(
            user,
            isPersistent: false);

        // --------------------------------------------------------
        // Merge Guest Cart
        // --------------------------------------------------------

        if (Request.Cookies["guest_cart"] is { } guest
            && Guid.TryParse(guest, out _))
        {
            await carts.MergeAsync(
                "g:" + guest,
                user.Id);
        }

        Response.Cookies.Delete("guest_cart");

        // --------------------------------------------------------
        // Audit
        // --------------------------------------------------------

        db.Add(new AuditEntry
        {
            ActorId = user.Id,
            Action = "Login",
            EntityType = "IdentityUser",
            EntityId = user.Id
        });

        await db.SaveChangesAsync();

        // --------------------------------------------------------
        // Redirect
        // --------------------------------------------------------

        return Redirect(
            model.Admin
                ? "/admin"
                : "/");
    }

    // ============================================================
    // CUSTOMER REGISTER
    //
    // Public registration ALWAYS = CUSTOMER
    // ============================================================

    [HttpGet("register")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterVm());
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // --------------------------------------------------------
        // IMPORTANT
        // Fixes:
        // Role CUSTOMER does not exist
        // --------------------------------------------------------

        var roleResult =
            await EnsureRoleAsync(CustomerRole);

        if (!roleResult.Succeeded)
        {
            AddIdentityErrors(roleResult);
            return View(model);
        }

        var email = model.Email.Trim();

        // --------------------------------------------------------
        // Duplicate Email
        // --------------------------------------------------------

        if (await users.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "An account already exists with this email.");

            return View(model);
        }

        // --------------------------------------------------------
        // Duplicate Phone
        // --------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(model.Phone))
        {
            var phoneExists =
                await users.Users.AnyAsync(x =>
                    x.PhoneNumber == model.Phone);

            if (phoneExists)
            {
                ModelState.AddModelError(
                    nameof(model.Phone),
                    "An account already exists with this phone number.");

                return View(model);
            }
        }

        // --------------------------------------------------------
        // Create Customer
        // --------------------------------------------------------

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = model.Phone?.Trim()
        };

        var createResult =
            await users.CreateAsync(
                user,
                model.Password);

        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        // --------------------------------------------------------
        // Assign CUSTOMER
        // --------------------------------------------------------

        var addRoleResult =
            await users.AddToRoleAsync(
                user,
                CustomerRole);

        if (!addRoleResult.Succeeded)
        {
            // Don't leave an incomplete user.
            await users.DeleteAsync(user);

            AddIdentityErrors(addRoleResult);

            return View(model);
        }

        // --------------------------------------------------------
        // Sign Customer In
        // --------------------------------------------------------

        await signIn.SignInAsync(
            user,
            isPersistent: false);

        // --------------------------------------------------------
        // Merge Guest Cart
        // --------------------------------------------------------

        if (Request.Cookies["guest_cart"] is { } guest
            && Guid.TryParse(guest, out _))
        {
            await carts.MergeAsync(
                "g:" + guest,
                user.Id);
        }

        Response.Cookies.Delete("guest_cart");

        // --------------------------------------------------------
        // Audit
        // --------------------------------------------------------

        db.Add(new AuditEntry
        {
            ActorId = user.Id,
            Action = "CustomerRegister",
            EntityType = "IdentityUser",
            EntityId = user.Id
        });

        await db.SaveChangesAsync();

        return Redirect("/account/profile");
    }

    // ============================================================
    // CREATE STAFF
    //
    // ADMIN only
    // ============================================================

    [HttpGet("staff/create")]
    [Authorize(Roles = AdminRole)]
    public IActionResult CreateStaff()
    {
        return View(new StaffRegisterVm());
    }

    [HttpPost("staff/create")]
    [Authorize(Roles = AdminRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStaff(
        StaffRegisterVm model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var roleResult =
            await EnsureRoleAsync(StaffRole);

        if (!roleResult.Succeeded)
        {
            AddIdentityErrors(roleResult);
            return View(model);
        }

        var email = model.Email.Trim();

        if (await users.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "This email is already registered.");

            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.Phone))
        {
            var phoneExists =
                await users.Users.AnyAsync(x =>
                    x.PhoneNumber == model.Phone);

            if (phoneExists)
            {
                ModelState.AddModelError(
                    nameof(model.Phone),
                    "This phone number is already registered.");

                return View(model);
            }
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = model.Phone?.Trim(),
            EmailConfirmed = true
        };

        var createResult =
            await users.CreateAsync(
                user,
                model.Password);

        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        var addRoleResult =
            await users.AddToRoleAsync(
                user,
                StaffRole);

        if (!addRoleResult.Succeeded)
        {
            await users.DeleteAsync(user);

            AddIdentityErrors(addRoleResult);

            return View(model);
        }

        db.Add(new AuditEntry
        {
            ActorId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier) ?? "system",

            Action = "CreateStaff",

            EntityType = "IdentityUser",

            EntityId = user.Id
        });

        await db.SaveChangesAsync();

        TempData["Message"] =
            "Staff account created successfully.";

        return Redirect("/admin");
    }

    // ============================================================
    // CREATE ADMIN
    //
    // Existing ADMIN only.
    // Never make this public.
    // ============================================================

    [HttpGet("admin/create")]
    [Authorize(Roles = AdminRole)]
    public IActionResult CreateAdmin()
    {
        return View(new StaffRegisterVm());
    }

    [HttpPost("admin/create")]
    [Authorize(Roles = AdminRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdmin(
        StaffRegisterVm model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var roleResult =
            await EnsureRoleAsync(AdminRole);

        if (!roleResult.Succeeded)
        {
            AddIdentityErrors(roleResult);
            return View(model);
        }

        var email = model.Email.Trim();

        if (await users.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "This email is already registered.");

            return View(model);
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = model.Phone?.Trim(),
            EmailConfirmed = true
        };

        var createResult =
            await users.CreateAsync(
                user,
                model.Password);

        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        var addRoleResult =
            await users.AddToRoleAsync(
                user,
                AdminRole);

        if (!addRoleResult.Succeeded)
        {
            await users.DeleteAsync(user);

            AddIdentityErrors(addRoleResult);

            return View(model);
        }

        db.Add(new AuditEntry
        {
            ActorId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier) ?? "system",

            Action = "CreateAdmin",

            EntityType = "IdentityUser",

            EntityId = user.Id
        });

        await db.SaveChangesAsync();

        TempData["Message"] =
            "Administrator account created successfully.";

        return Redirect("/admin");
    }

    // ============================================================
    // LOGOUT
    // ============================================================

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();

        return Redirect("/");
    }

    // ============================================================
    // PROFILE
    // ============================================================

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)!;

        ViewBag.StoreCredit =
            await new StoreCreditService(
                db,
                new LedgerService(db))
            .BalanceAsync(userId);

        ViewBag.Addresses =
            await db.Set<Address>()
                .Where(x =>
                    x.UserId == userId)
                .ToListAsync();

        ViewBag.Notifications =
            await db.Set<Notification>()
                .Where(x =>
                    x.UserId == userId)
                .OrderByDescending(
                    x => x.CreatedAt)
                .Take(20)
                .ToListAsync();

        return View(
            await users.GetUserAsync(User));
    }

    // ============================================================
    // CREATE ADDRESS
    // ============================================================

    [HttpPost("address")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Address(
        [Bind(
            "Recipient,Phone,Street,District,Area")]
        Address a)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                "Complete the address fields.");
        }

        a.UserId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)!;

        db.Add(a);

        await db.SaveChangesAsync();

        return RedirectToAction(
            nameof(Profile));
    }

    // ============================================================
    // CHANGE PASSWORD
    // ============================================================

    [HttpPost("password")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Password(
        string currentPassword,
        string newPassword)
    {
        var user =
            await users.GetUserAsync(User);

        if (user == null)
            return Unauthorized();

        var result =
            await users.ChangePasswordAsync(
                user,
                currentPassword,
                newPassword);

        TempData["Message"] =
            result.Succeeded
                ? "Password changed."
                : string.Join(
                    " ",
                    result.Errors.Select(
                        x => x.Description));

        if (result.Succeeded)
        {
            await signIn.RefreshSignInAsync(user);
        }

        return RedirectToAction(
            nameof(Profile));
    }

    // ============================================================
    // ACCESS DENIED
    // ============================================================

    [HttpGet("denied")]
    [AllowAnonymous]
    public IActionResult Denied()
    {
        return StatusCode(
            StatusCodes.Status403Forbidden,
            "You do not have permission for this operation.");
    }

    // ============================================================
    // UPDATE ADDRESS
    // ============================================================

    [HttpPost("address-update")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAddress(
        Guid id,
        string recipient,
        string phone,
        string street,
        string district,
        string area,
        bool isDefault)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)!;

        var address =
            await db.Set<Address>()
                .SingleOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == userId);

        if (address == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(recipient)
            || string.IsNullOrWhiteSpace(phone)
            || string.IsNullOrWhiteSpace(street))
        {
            return BadRequest(
                "Name, phone and street required.");
        }

        await using var tx =
            await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

        if (isDefault)
        {
            var oldAddresses =
                await db.Set<Address>()
                    .Where(x =>
                        x.UserId == userId)
                    .ToListAsync();

            foreach (var old in oldAddresses)
            {
                old.IsDefault = false;
            }
        }

        address.Recipient = recipient;
        address.Phone = phone;
        address.Street = street;
        address.District = district ?? "";
        address.Area = area ?? "";
        address.IsDefault = isDefault;

        await db.SaveChangesAsync();

        await tx.CommitAsync();

        return RedirectToAction(
            nameof(Profile));
    }

    // ============================================================
    // DELETE ADDRESS
    // ============================================================

    [HttpPost("address-delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(
        Guid id)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)!;

        var address =
            await db.Set<Address>()
                .SingleOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == userId);

        if (address == null)
            return NotFound();

        db.Remove(address);

        await db.SaveChangesAsync();

        return RedirectToAction(
            nameof(Profile));
    }

    // ============================================================
    // ROLE HELPER
    //
    // THIS FIXES:
    // System.InvalidOperationException:
    // Role CUSTOMER does not exist.
    // ============================================================

    private async Task<IdentityResult> EnsureRoleAsync(
        string roleName)
    {
        if (await roles.RoleExistsAsync(roleName))
        {
            return IdentityResult.Success;
        }

        return await roles.CreateAsync(
            new IdentityRole(roleName));
    }

    // ============================================================
    // IDENTITY ERROR HELPER
    // ============================================================

    private void AddIdentityErrors(
        IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(
                "",
                error.Description);
        }
    }
}