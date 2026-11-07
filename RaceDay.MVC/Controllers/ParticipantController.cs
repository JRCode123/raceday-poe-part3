using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Services;

namespace RaceDay.MVC.Controllers;

/// <summary>
/// All Participant-only pages:
/// - Browse/filter events
/// - Event detail + enrol
/// - My enrolments
/// - My race results history
/// </summary>
public class ParticipantController : Controller
{
    private readonly RaceDayApiService _api;

    public ParticipantController(RaceDayApiService api) => _api = api;

    // ── Guard: redirect non-Participants away ─────────────────────────────────
    private IActionResult? GuardParticipant()
    {
        if (HttpContext.Session.GetString("UserId") == null)
            return RedirectToAction("Login", "Account",
                new { returnUrl = Request.Path });
        if (HttpContext.Session.GetString("UserRole") != "Participant")
        {
            TempData["Error"] = "Access denied — Participants only.";
            return RedirectToAction("Dashboard", "Organiser");
        }
        return null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EVENTS — GET /Participant/Events
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Events(string? sport, string? search)
    {
        // Public page — no login required
        var events = await _api.GetAllEventsAsync();

        // Client-side filtering
        if (!string.IsNullOrWhiteSpace(sport))
            events = events.Where(e => e.Category?.Sport == sport).ToList();

        if (!string.IsNullOrWhiteSpace(search))
            events = events.Where(e =>
                e.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Location.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        ViewBag.Sport = sport;
        ViewBag.Search = search;
        return View(events);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EVENT DETAILS & ENROL — GET /Participant/EventDetails/{id}
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> EventDetails(int id)
    {
        var ev = await _api.GetEventAsync(id);
        if (ev == null)
        {
            TempData["Error"] = "Event not found.";
            return RedirectToAction("Events");
        }

        if (HttpContext.Session.GetString("UserRole") == "Participant")
        {
            var myEnrolments = await _api.GetMyEnrolmentsAsync();
            ev.AlreadyEnrolled = myEnrolments.Any(e => e.Event?.EventId == id);
        }

        return View(ev);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENROL — POST /Participant/Enrol/{eventId}
    // ══════════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enrol(int id)
    {
        if (GuardParticipant() is { } redirect) return redirect;

        var (success, message) = await _api.EnrolInEventAsync(id);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction("EventDetails", new { id });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MY ENROLMENTS — GET /Participant/MyEnrolments
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> MyEnrolments()
    {
        if (GuardParticipant() is { } redirect) return redirect;

        var enrolments = await _api.GetMyEnrolmentsAsync();
        return View(enrolments);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CANCEL ENROLMENT — POST /Participant/CancelEnrolment/{id}
    // ══════════════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        if (GuardParticipant() is { } redirect) return redirect;

        var (success, message) = await _api.CancelEnrolmentAsync(id);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction("MyEnrolments");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MY RESULTS — GET /Participant/MyResults
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> MyResults()
    {
        if (GuardParticipant() is { } redirect) return redirect;

        var results = await _api.GetMyResultsAsync();
        return View(results);
    }
}
