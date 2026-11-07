using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RaceDay.MVC.Models;
using System.Net.Http.Headers;
using System.Text;

namespace RaceDay.MVC.Services;

/// <summary>
/// Wraps every call to the Part 2 RaceDay REST API.
/// Uses HttpRequestMessage per call so the session cookie is set
/// on each individual request — not on shared DefaultRequestHeaders.
/// </summary>
public class RaceDayApiService
{
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ILogger<RaceDayApiService> _logger;
    private readonly string _baseUrl;
    private const string ApiCookieKey = "ApiSessionCookie";

    public RaceDayApiService(
        IConfiguration config,
        IHttpContextAccessor contextAccessor,
        ILogger<RaceDayApiService> logger)
    {
        _contextAccessor = contextAccessor;
        _logger = logger;
        _baseUrl = config["ApiSettings:BaseUrl"] ?? "http://localhost:5000/";
    }

    // ── Core helpers ──────────────────────────────────────────────────────────

    /// <summary>Creates a fresh HttpClient that bypasses self-signed cert errors.</summary>
    private HttpClient MakeClient()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false,
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri(_baseUrl) };
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }

    /// <summary>
    /// Builds an HttpRequestMessage with the stored API session cookie
    /// added directly to this specific request's headers.
    /// </summary>
    private HttpRequestMessage MakeRequest(HttpMethod method, string url, HttpContent? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body != null) request.Content = body;

        var cookie = _contextAccessor.HttpContext?.Session.GetString(ApiCookieKey);
        if (!string.IsNullOrEmpty(cookie))
            request.Headers.Add("Cookie", cookie);

        return request;
    }

    /// <summary>
    /// After a successful login the API sets a session cookie.
    /// Capture ALL Set-Cookie values and store them for future requests.
    /// </summary>
    private void CaptureCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies)) return;

        var all = cookies
            .Select(c => c.Split(';')[0].Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

        if (all.Any())
        {
            var header = string.Join("; ", all);
            _contextAccessor.HttpContext?.Session.SetString(ApiCookieKey, header);
            _logger.LogDebug("Captured API cookies: {Cookies}", header);
        }
    }

    private static StringContent JsonBody(object obj) =>
        new(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");

    private static string? Msg(string json)
    {
        try { return JObject.Parse(json)?["message"]?.ToString(); }
        catch { return null; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AUTH
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<(bool Success, string Message, string? UserId, string? Role, string? FullName)>
        LoginAsync(string email, string password)
    {
        try
        {
            using var client = MakeClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
            {
                Content = JsonBody(new { email, password })
            };
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                CaptureCookie(response);
                var j = JObject.Parse(content);
                return (true, "Login successful.",
                    j["userId"]?.ToString(),
                    j["role"]?.ToString(),
                    j["fullName"]?.ToString());
            }
            return (false, Msg(content) ?? "Login failed.", null, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoginAsync failed");
            return (false, "Cannot connect to the API.", null, null, null);
        }
    }

    public async Task<(bool Success, string Message)> RegisterAsync(RegisterViewModel vm)
    {
        try
        {
            using var client = MakeClient();
            var payload = new
            {
                fullName = vm.FullName, email = vm.Email, password = vm.Password,
                role = vm.Role, organisation = vm.Organisation,
                contactPhone = vm.ContactPhone, dateOfBirth = vm.DateOfBirth,
                gender = vm.Gender, emergencyContact = vm.EmergencyContact
            };
            var response = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, "api/auth/register")
                { Content = JsonBody(payload) });
            var content = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, "Account created successfully.")
                : (false, Msg(content) ?? "Registration failed.");
        }
        catch (Exception ex) { _logger.LogError(ex, "RegisterAsync failed"); return (false, "API error."); }
    }

    public async Task LogoutAsync()
    {
        try
        {
            using var client = MakeClient();
            await client.SendAsync(MakeRequest(HttpMethod.Post, "api/auth/logout"));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "LogoutAsync failed"); }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // USER PROFILE
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<ProfileViewModel?> GetProfileAsync()
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(MakeRequest(HttpMethod.Get, "api/users/profile"));
            if (!response.IsSuccessStatusCode) return null;
            var j = JObject.Parse(await response.Content.ReadAsStringAsync());
            return new ProfileViewModel
            {
                UserId = j["userId"]?.Value<int>() ?? 0,
                FullName = j["fullName"]?.ToString() ?? "",
                Email = j["email"]?.ToString() ?? "",
                Role = j["role"]?.ToString() ?? "",
                CreatedAt = j["createdAt"]?.ToString(),
                Organisation = j["organisation"]?.ToString(),
                ContactPhone = j["contactPhone"]?.ToString(),
                DateOfBirth = j["dateOfBirth"]?.ToString(),
                Gender = j["gender"]?.ToString(),
                EmergencyContact = j["emergencyContact"]?.ToString(),
                ProfilePictureUrl = j["profilePictureUrl"]?.ToString()
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "GetProfileAsync failed"); return null; }
    }

    public async Task<(bool Success, string Message)> UpdateProfileAsync(ProfileViewModel vm)
    {
        try
        {
            using var client = MakeClient();
            var payload = new
            {
                fullName = vm.FullName, organisation = vm.Organisation,
                contactPhone = vm.ContactPhone, emergencyContact = vm.EmergencyContact,
                gender = vm.Gender
            };
            var response = await client.SendAsync(
                MakeRequest(HttpMethod.Put, "api/users/profile", JsonBody(payload)));
            var content = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, "Profile updated.") : (false, Msg(content) ?? "Update failed.");
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdateProfileAsync failed"); return (false, "API error."); }
    }

    public async Task<(bool Success, string? ImageUrl, string Message)>
        UploadProfilePictureAsync(IFormFile file)
    {
        try
        {
            using var client = MakeClient();
            using var form = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream();
            var fc = new StreamContent(stream);
            fc.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            form.Add(fc, "file", file.FileName);
            var request = MakeRequest(HttpMethod.Post, "api/users/profile-picture", form);
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return (false, null, Msg(content) ?? "Upload failed.");
            var j = JObject.Parse(content);
            return (true, j["imageUrl"]?.ToString(), "Profile picture uploaded.");
        }
        catch (Exception ex) { _logger.LogError(ex, "UploadProfilePicture failed"); return (false, null, "API error."); }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EVENTS
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<List<EventListItem>> GetAllEventsAsync()
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(MakeRequest(HttpMethod.Get, "api/events"));
            if (!response.IsSuccessStatusCode) return new();
            var arr = JArray.Parse(await response.Content.ReadAsStringAsync());
            return arr.Select(MapEvent).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAllEventsAsync failed"); return new(); }
    }

    public async Task<EventDetailViewModel?> GetEventAsync(int id)
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(MakeRequest(HttpMethod.Get, $"api/events/{id}"));
            if (!response.IsSuccessStatusCode) return null;
            var j = JObject.Parse(await response.Content.ReadAsStringAsync());
            return new EventDetailViewModel
            {
                EventId = j["eventId"]?.Value<int>() ?? 0,
                Name = j["name"]?.ToString() ?? "",
                Description = j["description"]?.ToString(),
                Location = j["location"]?.ToString() ?? "",
                Route = j["route"]?.ToString(),
                EventDate = j["eventDate"]?.ToString() ?? "",
                StartTime = j["startTime"]?.ToString() ?? "",
                EntryFee = j["entryFee"]?.Value<decimal>() ?? 0,
                MaxParticipants = j["maxParticipants"]?.Value<int>() ?? 500,
                IsActive = j["isActive"]?.Value<bool>() ?? true,
                BannerImageUrl = j["bannerImageUrl"]?.ToString(),
                EnrolmentCount = j["enrolmentCount"]?.Value<int>() ?? 0,
                Category = j["category"] != null ? new CategoryDetail
                {
                    CategoryID = j["category"]?["categoryID"]?.Value<int>() ?? 0,
                    Name = j["category"]?["name"]?.ToString() ?? "",
                    Sport = j["category"]?["sport"]?.ToString() ?? "",
                    Description = j["category"]?["description"]?.ToString()
                } : null,
                Organiser = j["organiser"] != null ? new OrganiserDetail
                {
                    OrganiserID = j["organiser"]?["organiserID"]?.Value<int>() ?? 0,
                    Organisation = j["organiser"]?["organisation"]?.ToString() ?? "",
                    ContactPhone = j["organiser"]?["contactPhone"]?.ToString()
                } : null
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "GetEventAsync {id} failed", id); return null; }
    }

    public async Task<(bool Success, int EventId, string Message)> CreateEventAsync(CreateEventViewModel vm)
    {
        try
        {
            using var client = MakeClient();
            var payload = new
            {
                categoryID = vm.CategoryID, name = vm.Name, description = vm.Description,
                location = vm.Location, route = vm.Route, eventDate = vm.EventDate,
                startTime = vm.StartTime, maxParticipants = vm.MaxParticipants, entryFee = vm.EntryFee
            };
            var response = await client.SendAsync(
                MakeRequest(HttpMethod.Post, "api/events", JsonBody(payload)));
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return (false, 0, Msg(content) ?? "Failed to create event.");
            var j = JObject.Parse(content);
            return (true, j["eventId"]?.Value<int>() ?? 0, "Event created.");
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateEventAsync failed"); return (false, 0, "API error."); }
    }

    public async Task<(bool Success, string Message)> UpdateEventAsync(int id, EditEventViewModel vm)
    {
        try
        {
            using var client = MakeClient();
            var payload = new
            {
                categoryID = vm.CategoryID, name = vm.Name, description = vm.Description,
                location = vm.Location, route = vm.Route, eventDate = vm.EventDate,
                startTime = vm.StartTime, maxParticipants = vm.MaxParticipants,
                entryFee = vm.EntryFee, isActive = vm.IsActive
            };
            var response = await client.SendAsync(
                MakeRequest(HttpMethod.Put, $"api/events/{id}", JsonBody(payload)));
            var content = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, "Event updated.") : (false, Msg(content) ?? "Update failed.");
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdateEventAsync failed"); return (false, "API error."); }
    }

    public async Task<(bool Success, string Message)> DeleteEventAsync(int id)
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(MakeRequest(HttpMethod.Delete, $"api/events/{id}"));
            var content = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, "Event deleted.") : (false, Msg(content) ?? "Delete failed.");
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteEventAsync failed"); return (false, "API error."); }
    }

    public async Task<(bool Success, string? ImageUrl, string Message)>
        UploadEventBannerAsync(int eventId, IFormFile file)
    {
        try
        {
            using var client = MakeClient();
            using var form = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream();
            var fc = new StreamContent(stream);
            fc.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            form.Add(fc, "file", file.FileName);
            var request = MakeRequest(HttpMethod.Post, $"api/events/{eventId}/image", form);
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return (false, null, Msg(content) ?? "Upload failed.");
            var j = JObject.Parse(content);
            return (true, j["imageUrl"]?.ToString(), "Banner uploaded.");
        }
        catch (Exception ex) { _logger.LogError(ex, "UploadEventBanner failed"); return (false, null, "API error."); }
    }

    public async Task<EventEnrolmentsViewModel?> GetEventEnrolmentsAsync(int eventId)
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(
                MakeRequest(HttpMethod.Get, $"api/events/{eventId}/enrolments"));
            if (!response.IsSuccessStatusCode) return null;
            var j = JObject.Parse(await response.Content.ReadAsStringAsync());
            var enrolments = (j["enrolments"] as JArray ?? new JArray())
                .Select(e => new EventEnrolmentItem
                {
                    EnrolmentId = e["enrolmentId"]?.Value<int>() ?? 0,
                    ParticipantId = e["participantId"]?.Value<int>() ?? 0,
                    ParticipantName = e["participantName"]?.ToString() ?? "",
                    ParticipantEmail = e["participantEmail"]?.ToString() ?? "",
                    EnrolledAt = e["enrolledAt"]?.Value<DateTime>() ?? DateTime.Now,
                    Status = e["status"]?.ToString() ?? "",
                    HasResult = e["hasResult"]?.Value<bool>() ?? false,
                    FinishTime = e["finishTime"]?.ToString(),
                    Position = e["position"]?.Value<int?>()
                }).ToList();
            return new EventEnrolmentsViewModel
            {
                EventId = eventId,
                EventName = j["eventName"]?.ToString() ?? "",
                TotalEnrolments = j["totalEnrolments"]?.Value<int>() ?? 0,
                Enrolments = enrolments
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "GetEventEnrolments failed"); return null; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CATEGORIES
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<List<CategoryListItem>> GetAllCategoriesAsync()
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(MakeRequest(HttpMethod.Get, "api/categories"));
            if (!response.IsSuccessStatusCode) return new();
            var arr = JArray.Parse(await response.Content.ReadAsStringAsync());
            return arr.Select(c => new CategoryListItem
            {
                CategoryId = c["categoryId"]?.Value<int>() ?? 0,
                Name = c["name"]?.ToString() ?? "",
                Description = c["description"]?.ToString(),
                Sport = c["sport"]?.ToString() ?? "",
                Organiser = c["organiser"]?.ToString(),
                EventCount = c["eventCount"]?.Value<int>() ?? 0
            }).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAllCategoriesAsync failed"); return new(); }
    }

    public async Task<(bool Success, string Message)> CreateCategoryAsync(CreateCategoryViewModel vm)
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(MakeRequest(HttpMethod.Post, "api/categories",
                JsonBody(new { name = vm.Name, description = vm.Description, sport = vm.Sport })));
            var content = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, "Category created.") : (false, Msg(content) ?? "Failed.");
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateCategoryAsync failed"); return (false, "API error."); }
    }

    public async Task<(bool Success, string Message)> DeleteCategoryAsync(int id)
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(MakeRequest(HttpMethod.Delete, $"api/categories/{id}"));
            var content = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, "Category deleted.") : (false, Msg(content) ?? "Cannot delete.");
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteCategoryAsync failed"); return (false, "API error."); }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ENROLMENTS
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<(bool Success, string Message)> EnrolInEventAsync(int eventId)
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(MakeRequest(HttpMethod.Post, "api/enrolments",
                JsonBody(new { eventID = eventId })));
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("EnrolInEvent {id} → {status}: {body}", eventId, response.StatusCode, content);
            return response.IsSuccessStatusCode
                ? (true, Msg(content) ?? "Enrolled successfully.")
                : (false, Msg(content) ?? "Enrolment failed.");
        }
        catch (Exception ex) { _logger.LogError(ex, "EnrolInEventAsync failed"); return (false, "API error."); }
    }

    public async Task<List<MyEnrolmentItem>> GetMyEnrolmentsAsync()
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(MakeRequest(HttpMethod.Get, "api/enrolments/my"));
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("GetMyEnrolments → {status}: {body}", response.StatusCode, content);

            if (!response.IsSuccessStatusCode) return new();

            var j = JObject.Parse(content);
            var arr = j["enrolments"] as JArray ?? new JArray();
            return arr.Select(e => new MyEnrolmentItem
            {
                EnrolmentId = e["enrolmentId"]?.Value<int>() ?? 0,
                EnrolledAt = e["enrolledAt"]?.ToString() ?? "",
                Status = e["status"]?.ToString() ?? "",
                Event = e["event"] != null ? new EnrolmentEventSummary
                {
                    EventId = e["event"]?["eventId"]?.Value<int>() ?? 0,
                    Name = e["event"]?["name"]?.ToString() ?? "",
                    EventDate = e["event"]?["eventDate"]?.ToString() ?? "",
                    Location = e["event"]?["location"]?.ToString() ?? "",
                    Category = e["event"]?["category"]?.ToString() ?? "",
                    Sport = e["event"]?["sport"]?.ToString() ?? ""
                } : null,
                Result = e["result"] != null && e["result"]?.Type != JTokenType.Null
                    ? new EnrolmentResult
                    {
                        FinishTime = e["result"]?["finishTime"]?.ToString() ?? "",
                        Position = e["result"]?["position"]?.Value<int?>(),
                        Notes = e["result"]?["notes"]?.ToString()
                    } : null
            }).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetMyEnrolmentsAsync failed"); return new(); }
    }

    public async Task<(bool Success, string Message)> CancelEnrolmentAsync(int enrolmentId)
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(
                MakeRequest(HttpMethod.Delete, $"api/enrolments/{enrolmentId}"));
            var content = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, "Enrolment cancelled.") : (false, Msg(content) ?? "Cannot cancel.");
        }
        catch (Exception ex) { _logger.LogError(ex, "CancelEnrolmentAsync failed"); return (false, "API error."); }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RESULTS
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<(bool Success, string Message)> CaptureResultAsync(CaptureResultViewModel vm)
    {
        try
        {
            using var client = MakeClient();
            var payload = new
            {
                enrolmentID = vm.EnrolmentID, finishTime = vm.FinishTime,
                position = vm.Position, notes = vm.Notes
            };
            var response = await client.SendAsync(
                MakeRequest(HttpMethod.Post, "api/results", JsonBody(payload)));
            var content = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, "Result captured.") : (false, Msg(content) ?? "Failed.");
        }
        catch (Exception ex) { _logger.LogError(ex, "CaptureResultAsync failed"); return (false, "API error."); }
    }

    public async Task<List<MyResultItem>> GetMyResultsAsync()
    {
        try
        {
            using var client = MakeClient();
            var response = await client.SendAsync(MakeRequest(HttpMethod.Get, "api/results/my"));
            if (!response.IsSuccessStatusCode) return new();
            var j = JObject.Parse(await response.Content.ReadAsStringAsync());
            var arr = j["results"] as JArray ?? new JArray();
            return arr.Select(r => new MyResultItem
            {
                ResultId = r["resultId"]?.Value<int>() ?? 0,
                EventName = r["eventName"]?.ToString() ?? "",
                EventDate = r["eventDate"]?.ToString() ?? "",
                Location = r["location"]?.ToString() ?? "",
                Category = r["category"]?.ToString() ?? "",
                Sport = r["sport"]?.ToString() ?? "",
                FinishTime = r["finishTime"]?.ToString() ?? "",
                Position = r["position"]?.Value<int?>(),
                Notes = r["notes"]?.ToString()
            }).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetMyResultsAsync failed"); return new(); }
    }

    // ── Shared mapper ──────────────────────────────────────────────────────────

    private static EventListItem MapEvent(JToken j) => new()
    {
        EventId = j["eventId"]?.Value<int>() ?? 0,
        Name = j["name"]?.ToString() ?? "",
        Description = j["description"]?.ToString(),
        Location = j["location"]?.ToString() ?? "",
        EventDate = j["eventDate"]?.ToString() ?? "",
        StartTime = j["startTime"]?.ToString() ?? "",
        EntryFee = j["entryFee"]?.Value<decimal>() ?? 0,
        MaxParticipants = j["maxParticipants"]?.Value<int>() ?? 500,
        IsActive = j["isActive"]?.Value<bool>() ?? true,
        BannerImageUrl = j["bannerImageUrl"]?.ToString(),
        Category = j["category"] != null ? new CategorySummary
        {
            CategoryID = j["category"]?["categoryID"]?.Value<int>() ?? 0,
            Name = j["category"]?["name"]?.ToString() ?? "",
            Sport = j["category"]?["sport"]?.ToString() ?? ""
        } : null,
        Organiser = j["organiser"] != null ? new OrganiserSummary
        {
            OrganiserID = j["organiser"]?["organiserID"]?.Value<int>() ?? 0,
            Organisation = j["organiser"]?["organisation"]?.ToString() ?? ""
        } : null
    };
}
