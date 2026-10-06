using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Web.Controllers
{
    // শুধু Admin / SuperAdmin seed চালাতে পারবে
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class SeedController : Controller
    {
        private readonly SeedData _seedData;
        private readonly ILogger<SeedController> _logger;

        public SeedController(
            SeedData seedData,
            ILogger<SeedController> logger)
        {
            _seedData = seedData;
            _logger = logger;
        }


        // ==========================================
        // SEED PAGE
        // ==========================================
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }


        // ==========================================
        // RUN SEED
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Run()
        {
            try
            {
                // SeedData.cs handles:
                // 1. Roles
                // 2. Admin/SuperAdmin assignment
                // 3. Permissions
                // 4. Menu/MenuItem
                await _seedData.RunAsync();

                TempData["Success"] =
                    "Role, Admin, Permission and Menu seed completed successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Manual database seed failed.");

                TempData["Error"] =
                    "Seed failed: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}