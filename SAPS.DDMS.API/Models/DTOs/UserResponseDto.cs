namespace SAPS.DDMS.API.Models.DTOs;

public class UserResponseDto
{
    public int UserID { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName => $"{FirstName} {LastName}";
    public string? PersonnelNumber { get; set; }
    public string? Rank { get; set; }
    public string? EmailAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? RoleName { get; set; }
    public string? StationName { get; set; }
    public string? AccountStatus { get; set; }
    public string? NotificationPreferences { get; set; }
    public bool IsActivated { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public bool? ActivationEmailSent { get; set; }
    public string? ActivationLink { get; set; }
    public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();
}
