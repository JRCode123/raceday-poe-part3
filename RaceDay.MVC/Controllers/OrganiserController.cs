using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Models;
using RaceDay.MVC.Services;

namespace RaceDay.MVC.Controllers;

/// <summary>
/// All Organiser-only pages:
/// - Dashboard (summary stats + upcoming events)
/// - Event management (create, edit, delete, view enrolments)
/// - Category management
/// - Results entry (capture finish times after a race)
/// </summary>
public class OrganiserController : Controller
{
    private readonly RaceDayApiService _api;

    public OrganiserController(RaceDayApiService api) => _api = api;

    // ── Guard: redirect non-Organisers away ───────────────────────────────────
    private IActionResult? GuardOrganiser()
    {
        if (HttpContext.Session.GetString("UserId") == null)
            return RedirectToAction("Login", "Account",
                new { returnUrl = Request.Path });
        if (HttpContext.Session.GetString("UserRole") != "Organiser")
        {
            TempData["Error"] = "Access denied — Organisers only.";
            return RedirectToAction("Events", "Participant");
        }
        return null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DASHBOARD — GET /Organiser/Dashboard
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Dashboard()
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        var allEvents = await _api.GetAllEventsAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Count total enrolments from all events (approximate using public data)
        var vm = new OrganiserDashboardViewModel
        {
            OrgName = HttpContext.Session.GetString("UserName") ?? "Organiser",
            AllEvents = allEvents,
            TotalEvents = allEvents.Count,
            TotalEnrolments = allEvents.Sum(e => e.EnrolmentCount),
            UpcomingEvents = allEvents
                .Where(e => DateOnly.TryParse(e.EventDate, out var d) && d >= today)
                .OrderBy(e => e.EventDate)
                .Take(5)
                .ToList()
        };

        return View(vm);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EVENTS — GET /Organiser/Events
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Events()
    {
        if (GuardOrganiser() is { } redirect) return redirect;
        var events = await _api.GetAllEventsAsync();
        return View(events);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CREATE EVENT — GET/POST /Organiser/CreateEvent
    // ══════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> CreateEvent()
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        var categories = await _api.GetAllCategoriesAsync();
        var vm = new CreateEventViewModel
        {
            AvailableCategories = categories.Select(c => new CategorySummary
            {
                CategoryID = c.CategoryId,
                Name = c.Name,
                Sport = c.Sport
            }).ToList(),
            EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)).ToString("yyyy-MM-dd"),
            StartTime = "07:00"
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(CreateEventViewModel vm)
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        if (!ModelState.IsValid)
        {
            var cats = await _api.GetAllCategoriesAsync();
            vm.AvailableCategories = cats.Select(c => new CategorySummary
                { CategoryID = c.CategoryId, Name = c.Name, Sport = c.Sport }).ToList();
            return View(vm);
        }

        var (success, eventId, message) = await _api.CreateEventAsync(vm);

        if (!success)
        {
            ModelState.AddModelError("", message);
            var cats = await _api.GetAllCategoriesAsync();
            vm.AvailableCategories = cats.Select(c => new CategorySummary
                { CategoryID = c.CategoryId, Name = c.Name, Sport = c.Sport }).ToList();
            return View(vm);
        }

        // Upload banner image if provided
        if (vm.BannerImage != null && vm.BannerImage.Length > 0 && eventId > 0)
        {
            var (imgSuccess, _, imgMsg) = await _api.UploadEventBannerAsync(eventId, vm.BannerImage);
            if (!imgSuccess) TempData["Warning"] = $"Event created but banner upload failed: {imgMsg}";
        }

        TempData["Success"] = $"Event '{vm.Name}' created successfully!";
        return RedirectToAction("Events");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EDIT EVENT — GET/POST /Organiser/EditEvent/{id}
    // ══════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        var ev = await _api.GetEventAsync(id);
        if (ev == null) { TempData["Error"] = "Event not found."; return RedirectToAction("Events"); }

        var categories = await _api.GetAllCategoriesAsync();
        var vm = new EditEventViewModel
        {
            EventId = id,
            CategoryID = ev.Category?.CategoryID ?? 0,
            Name = ev.Name,
            Description = ev.Description,
            Location = ev.Location,
            Route = ev.Route,
            EventDate = ev.EventDate,
            StartTime = ev.StartTime,
            MaxParticipants = ev.MaxParticipants,
            EntryFee = ev.EntryFee,
            IsActive = ev.IsActive,
            ExistingBannerUrl = ev.BannerImageUrl,
            AvailableCategories = categories.Select(c => new CategorySummary
                { CategoryID = c.CategoryId, Name = c.Name, Sport = c.Sport }).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(int id, EditEventViewModel vm)
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        if (!ModelState.IsValid)
        {
            var cats = await _api.GetAllCategoriesAsync();
            vm.AvailableCategories = cats.Select(c => new CategorySummary
                { CategoryID = c.CategoryId, Name = c.Name, Sport = c.Sport }).ToList();
            return View(vm);
        }

        var (success, message) = await _api.UpdateEventAsync(id, vm);

        if (!success)
        {
            ModelState.AddModelError("", message);
            var cats = await _api.GetAllCategoriesAsync();
            vm.AvailableCategories = cats.Select(c => new CategorySummary
                { CategoryID = c.CategoryId, Name = c.Name, Sport = c.Sport }).ToList();
            return View(vm);
        }

        // Upload new banner if provided
        if (vm.BannerImage != null && vm.BannerImage.Length > 0)
        {
            var (imgSuccess, _, imgMsg) = await _api.UploadEventBannerAsync(id, vm.BannerImage);
            if (!imgSuccess) TempData["Warning"] = $"Event updated but banner upload failed: {imgMsg}";
        }

        TempData["Success"] = "Event updated successfully.";
        return RedirectToAction("Events");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DELETE EVENT — POST /Organiser/DeleteEvent/{id}
    // ══════════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        var (success, message) = await _api.DeleteEventAsync(id);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction("Events");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENROLMENTS — GET /Organiser/Enrolments/{eventId}
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Enrolments(int id)
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        var vm = await _api.GetEventEnrolmentsAsync(id);
        if (vm == null)
        {
            TempData["Error"] = "Could not load enrolments.";
            return RedirectToAction("Events");
        }
        return View(vm);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CATEGORIES — GET /Organiser/Categories
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Categories()
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        var categories = await _api.GetAllCategoriesAsync();
        ViewBag.NewCategory = new CreateCategoryViewModel();
        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(CreateCategoryViewModel vm)
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please fix the validation errors.";
            return RedirectToAction("Categories");
        }

        var (success, message) = await _api.CreateCategoryAsync(vm);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction("Categories");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        var (success, message) = await _api.DeleteCategoryAsync(id);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction("Categories");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RESULTS — GET/POST /Organiser/Results/{eventId}
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Results(int id)
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        var enrolments = await _api.GetEventEnrolmentsAsync(id);
        if (enrolments == null)
        {
            TempData["Error"] = "Could not load enrolments for results.";
            return RedirectToAction("Events");
        }

        ViewBag.EventId = id;
        ViewBag.EventName = enrolments.EventName;
        return View(enrolments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CaptureResult(CaptureResultViewModel vm, int eventId)
    {
        if (GuardOrganiser() is { } redirect) return redirect;

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a valid finish time.";
            return RedirectToAction("Results", new { id = eventId });
        }

        var (success, message) = await _api.CaptureResultAsync(vm);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction("Results", new { id = eventId });
    }
}
