// ── ADD THIS FILE TO: RaceDay.API/Controllers/UploadController.cs ───────────
// This controller handles all Azure Blob Storage file uploads.
// The MVC app NEVER talks to Azure directly — it posts files here and gets back a URL.
//
// ALSO add "BannerImageUrl" to RaceEvent.cs:
//   [MaxLength(500)]
//   public string? BannerImageUrl { get; set; }
//
// And "ProfilePictureUrl" to Participant.cs:
//   [MaxLength(500)]
//   public string? ProfilePictureUrl { get; set; }
//
// Then run: Add-Migration AddBlobImageFields
//          Update-Database
// ────────────────────────────────────────────────────────────────────────────

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Filters;
using RaceDay.API.Services;

namespace RaceDay.API.Controllers;

/// <summary>
/// Handles all file uploads via Azure Blob Storage.
/// Only the MVC app calls these endpoints — never the browser directly.
/// </summary>
[ApiController]
[Produces("application/json")]
public class UploadController : ControllerBase
{
    private readonly RaceDayDbContext  _db;
    private readonly BlobStorageService _blob;
    private static readonly string[] AllowedTypes =
        { "image/jpeg", "image/png", "image/webp" };
    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    public UploadController(RaceDayDbContext db, BlobStorageService blob)
    {
        _db   = db;
        _blob = blob;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // POST /api/events/{id}/image  —  Organiser only
    // Uploads a banner image and stores the URL on the event record.
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Upload an event banner image. Stored in Azure Blob Storage.
    /// Returns the public URL for the image.
    /// </summary>
    [HttpPost("api/events/{id:int}/image")]
    [SessionAuthorize(Role = "Organiser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadEventBanner(int id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (file.Length > MaxBytes)
            return BadRequest(new { message = "File is too large. Maximum size is 5 MB." });

        if (!AllowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { message = "Only JPG, PNG, and WebP images are allowed." });

        // Verify event exists and belongs to this organiser
        var userId   = int.Parse(HttpContext.Session.GetString("UserId")!);
        var organiser = await _db.Organisers.FirstOrDefaultAsync(o => o.UserID == userId);
        var ev        = await _db.Events.FindAsync(id);

        if (ev == null)
            return NotFound(new { message = "Event not found." });
        if (ev.OrganiserID != organiser?.OrganiserID)
            return StatusCode(403, new { message = "You can only upload images for your own events." });

        await using var stream = file.OpenReadStream();
        var imageUrl = await _blob.UploadAsync(stream, file.FileName, file.ContentType);

        if (imageUrl == null)
        {
            // Azure not configured — return a placeholder message but don't fail
            return Ok(new
            {
                imageUrl = (string?)null,
                message  = "Azure Blob Storage is not configured. Add connection string to appsettings.json."
            });
        }

        // Delete previous banner if any
        if (!string.IsNullOrEmpty(ev.BannerImageUrl))
            await _blob.DeleteAsync(ev.BannerImageUrl);

        ev.BannerImageUrl = imageUrl;
        await _db.SaveChangesAsync();

        return Ok(new { imageUrl, message = "Banner uploaded successfully." });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // POST /api/users/profile-picture  —  Any logged-in user
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Upload a profile picture. Stored in Azure Blob Storage.</summary>
    [HttpPost("api/users/profile-picture")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (file.Length > MaxBytes)
            return BadRequest(new { message = "File is too large. Maximum size is 5 MB." });

        if (!AllowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { message = "Only JPG, PNG, and WebP images are allowed." });

        var userId      = int.Parse(HttpContext.Session.GetString("UserId")!);
        var participant = await _db.Participants.FirstOrDefaultAsync(p => p.UserID == userId);

        if (participant == null)
            return BadRequest(new { message = "Profile pictures are only for Participant accounts." });

        await using var stream = file.OpenReadStream();
        var imageUrl = await _blob.UploadAsync(stream, file.FileName, file.ContentType);

        if (imageUrl == null)
        {
            return Ok(new
            {
                imageUrl = (string?)null,
                message  = "Azure Blob Storage is not configured."
            });
        }

        if (!string.IsNullOrEmpty(participant.ProfilePictureUrl))
            await _blob.DeleteAsync(participant.ProfilePictureUrl);

        participant.ProfilePictureUrl = imageUrl;
        await _db.SaveChangesAsync();

        return Ok(new { imageUrl, message = "Profile picture updated." });
    }
}

// UploadController: event banner and profile picture endpoints
