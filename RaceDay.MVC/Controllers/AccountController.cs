using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Models;
using RaceDay.MVC.Services;

namespace RaceDay.MVC.Controllers;

/// <summary>
/// Handles login, registration, logout, and profile management.
/// Authentication is session-based via the Part 2 API.
/// </summary>
public class AccountController : Controller
{
    private readonly RaceDayApiService _api;

    public AccountController(RaceDayApiService api) => _api = api;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsLoggedIn => HttpContext.Session.GetString("UserId") != null;
    private string? UserRole => HttpContext.Session.GetString("UserRole");

    private void SetSession(string userId, string role, string fullName)
    {
        HttpContext.Session.SetString("UserId", userId);
        HttpContext.Session.SetString("UserRole", role);
        HttpContext.Session.SetString("UserName", fullName);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GET /Account/Login
    // ══════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public IActionResult Login()
    {
        if (IsLoggedIn) return RedirectBasedOnRole();
        return View();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // POST /Account/Login
    // ══════════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(vm);

        var (success, message, userId, role, fullName) =
            await _api.LoginAsync(vm.Email, vm.Password);

        if (!success)
        {
            ModelState.AddModelError("", message);
            return View(vm);
        }

        SetSession(userId!, role!, fullName!);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectBasedOnRole();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GET /Account/Register
    // ══════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public IActionResult Register()
    {
        if (IsLoggedIn) return RedirectBasedOnRole();
        return View(new RegisterViewModel());
    }

    // ══════════════════════════════════════════════════════════════════════════
    // POST /Account/Register
    // ══════════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        // Conditional required field validation
        if (vm.Role == "Organiser" && string.IsNullOrWhiteSpace(vm.Organisation))
            ModelState.AddModelError("Organisation", "Organisation name is required for Organisers.");

        if (!ModelState.IsValid) return View(vm);

        var (success, message) = await _api.RegisterAsync(vm);

        if (!success)
        {
            ModelState.AddModelError("", message);
            return View(vm);
        }

        TempData["Success"] = "Account created! Please log in.";
        return RedirectToAction("Login");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // POST /Account/Logout
    // ══════════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _api.LogoutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GET /Account/Profile
    // ══════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");

        var profile = await _api.GetProfileAsync();
        if (profile == null)
        {
            TempData["Error"] = "Could not load your profile.";
            return RedirectBasedOnRole();
        }
        return View(profile);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // POST /Account/Profile
    // ══════════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel vm)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");

        // Remove validation for fields not relevant to current role
        if (UserRole == "Organiser")
        {
            ModelState.Remove("Gender");
            ModelState.Remove("EmergencyContact");
        }
        else
        {
            ModelState.Remove("Organisation");
            ModelState.Remove("ContactPhone");
        }

        if (!ModelState.IsValid)
        {
            // Keep existing data for display
            var existing = await _api.GetProfileAsync();
            if (existing != null)
            {
                vm.Email = existing.Email;
                vm.Role = existing.Role;
                vm.ProfilePictureUrl = existing.ProfilePictureUrl;
            }
            return View(vm);
        }

        // Handle profile picture upload
        if (vm.ProfilePicture != null && vm.ProfilePicture.Length > 0)
        {
            var (picSuccess, imageUrl, picMsg) = await _api.UploadProfilePictureAsync(vm.ProfilePicture);
            if (!picSuccess)
                TempData["Warning"] = $"Profile saved but photo upload failed: {picMsg}";
        }

        var (success, message) = await _api.UpdateProfileAsync(vm);

        if (success)
        {
            HttpContext.Session.SetString("UserName", vm.FullName);
            TempData["Success"] = message;
        }
        else
        {
            TempData["Error"] = message;
        }

        return RedirectToAction("Profile");
    }

    // ── Helper: redirect based on role after login ────────────────────────────

    private IActionResult RedirectBasedOnRole()
    {
        return UserRole == "Organiser"
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Events", "Participant");
    }
}

// Account controller handles login, register and profile
