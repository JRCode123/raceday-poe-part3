using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models;

// ════════════════════════════════════════════════════════════════════════
// AUTH VIEW MODELS
// ════════════════════════════════════════════════════════════════════════

public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = "Participant"; // "Organiser" | "Participant"

    // Organiser-specific
    public string? Organisation { get; set; }
    [Display(Name = "Contact Phone")]
    public string? ContactPhone { get; set; }

    // Participant-specific
    [Display(Name = "Date of Birth")]
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    [Display(Name = "Emergency Contact")]
    public string? EmergencyContact { get; set; }
}

// ════════════════════════════════════════════════════════════════════════
// PROFILE VIEW MODELS
// ════════════════════════════════════════════════════════════════════════

public class ProfileViewModel
{
    public int UserId { get; set; }
    [Display(Name = "Full Name")]
    [Required(ErrorMessage = "Full name is required.")]
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? CreatedAt { get; set; }

    // Organiser
    public string? Organisation { get; set; }
    [Display(Name = "Contact Phone")]
    public string? ContactPhone { get; set; }

    // Participant
    [Display(Name = "Date of Birth")]
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    [Display(Name = "Emergency Contact")]
    public string? EmergencyContact { get; set; }
    [Display(Name = "Profile Picture")]
    public string? ProfilePictureUrl { get; set; }
    public IFormFile? ProfilePicture { get; set; }
}

// ════════════════════════════════════════════════════════════════════════
// EVENT VIEW MODELS
// ════════════════════════════════════════════════════════════════════════

public class EventListItem
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Location { get; set; } = string.Empty;
    public string EventDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }
    public bool IsActive { get; set; }
    public string? BannerImageUrl { get; set; }
    public CategorySummary? Category { get; set; }
    public OrganiserSummary? Organiser { get; set; }
    public int EnrolmentCount { get; set; }
}

public class EventDetailViewModel
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string EventDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }
    public bool IsActive { get; set; }
    public string? BannerImageUrl { get; set; }
    public CategoryDetail? Category { get; set; }
    public OrganiserDetail? Organiser { get; set; }
    public int EnrolmentCount { get; set; }
    public bool AlreadyEnrolled { get; set; }
}

public class CategorySummary
{
    public int CategoryID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sport { get; set; } = string.Empty;
}

public class CategoryDetail : CategorySummary
{
    public string? Description { get; set; }
}

public class OrganiserSummary
{
    public int OrganiserID { get; set; }
    public string Organisation { get; set; } = string.Empty;
}

public class OrganiserDetail : OrganiserSummary
{
    public string? ContactPhone { get; set; }
    public string? OrganizerName { get; set; }
}

public class CreateEventViewModel
{
    [Required(ErrorMessage = "Event name is required.")]
    [MaxLength(150)]
    [Display(Name = "Event Name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Location is required.")]
    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Route { get; set; }

    [Required(ErrorMessage = "Event date is required.")]
    [Display(Name = "Event Date")]
    public string EventDate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Start time is required.")]
    [Display(Name = "Start Time")]
    public string StartTime { get; set; } = string.Empty;

    [Display(Name = "Max Participants")]
    public int MaxParticipants { get; set; } = 500;

    [Display(Name = "Entry Fee (R)")]
    public decimal EntryFee { get; set; } = 0.00m;

    [Required(ErrorMessage = "Category is required.")]
    [Display(Name = "Category")]
    public int CategoryID { get; set; }

    [Display(Name = "Event Banner Image")]
    public IFormFile? BannerImage { get; set; }

    public List<CategorySummary> AvailableCategories { get; set; } = new();
}

public class EditEventViewModel : CreateEventViewModel
{
    public int EventId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ExistingBannerUrl { get; set; }
}

// ════════════════════════════════════════════════════════════════════════
// CATEGORY VIEW MODELS
// ════════════════════════════════════════════════════════════════════════

public class CategoryListItem
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Sport { get; set; } = string.Empty;
    public string? Organiser { get; set; }
    public int EventCount { get; set; }
}

public class CreateCategoryViewModel
{
    [Required(ErrorMessage = "Category name is required.")]
    [MaxLength(100)]
    [Display(Name = "Category Name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Sport type is required.")]
    public string Sport { get; set; } = "Running";
}

// ════════════════════════════════════════════════════════════════════════
// ENROLMENT VIEW MODELS
// ════════════════════════════════════════════════════════════════════════

public class MyEnrolmentItem
{
    public int EnrolmentId { get; set; }
    public string EnrolledAt { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public EnrolmentEventSummary? Event { get; set; }
    public EnrolmentResult? Result { get; set; }
}

public class EnrolmentEventSummary
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EventDate { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Sport { get; set; } = string.Empty;
}

public class EnrolmentResult
{
    public string FinishTime { get; set; } = string.Empty;
    public int? Position { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class EventEnrolmentItem
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string ParticipantEmail { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasResult { get; set; }
    public string? FinishTime { get; set; }
    public int? Position { get; set; }
}

public class EventEnrolmentsViewModel
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int TotalEnrolments { get; set; }
    public List<EventEnrolmentItem> Enrolments { get; set; } = new();
}

// ════════════════════════════════════════════════════════════════════════
// RESULT VIEW MODELS
// ════════════════════════════════════════════════════════════════════════

public class MyResultItem
{
    public int ResultId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string EventDate { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Sport { get; set; } = string.Empty;
    public string FinishTime { get; set; } = string.Empty;
    public int? Position { get; set; }
    public string? Notes { get; set; }
}

public class CaptureResultViewModel
{
    [Required(ErrorMessage = "Enrolment ID is required.")]
    public int EnrolmentID { get; set; }

    [Required(ErrorMessage = "Finish time is required (e.g. 2:45:30).")]
    [Display(Name = "Finish Time")]
    public string FinishTime { get; set; } = string.Empty;

    public int? Position { get; set; }
    public string? Notes { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
}

// ════════════════════════════════════════════════════════════════════════
// DASHBOARD VIEW MODEL
// ════════════════════════════════════════════════════════════════════════

public class OrganiserDashboardViewModel
{
    public string OrgName { get; set; } = string.Empty;
    public int TotalEvents { get; set; }
    public int TotalEnrolments { get; set; }
    public List<EventListItem> UpcomingEvents { get; set; } = new();
    public List<EventListItem> AllEvents { get; set; } = new();
}
