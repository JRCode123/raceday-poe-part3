using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Services;

namespace RaceDay.MVC.Controllers;

/// <summary>
/// Public home page — shows all upcoming events without requiring login.
/// </summary>
public class HomeController : Controller
{
    private readonly RaceDayApiService _api;

    public HomeController(RaceDayApiService api) => _api = api;

    public async Task<IActionResult> Index()
    {
        var events = await _api.GetAllEventsAsync();
        return View(events);
    }

    // Event detail accessible to everyone — login required only to enrol
    public async Task<IActionResult> EventDetails(int id)
    {
        var ev = await _api.GetEventAsync(id);
        if (ev == null)
        {
            TempData["Error"] = "Event not found.";
            return RedirectToAction("Index");
        }

        // Check if the logged-in participant is already enrolled
        if (HttpContext.Session.GetString("UserRole") == "Participant")
        {
            var myEnrolments = await _api.GetMyEnrolmentsAsync();
            ev.AlreadyEnrolled = myEnrolments.Any(e => e.Event?.EventId == id);
        }

        return View(ev);
    }

    public IActionResult Error() => View();
}
