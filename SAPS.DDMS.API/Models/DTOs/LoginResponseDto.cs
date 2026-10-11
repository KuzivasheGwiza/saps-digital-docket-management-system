using Microsoft.Identity.Client;

namespace SAPS.DDMS.API.Models.DTOs
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public UserInfoDto? User { get; set; } = new();
        public DateTime? ExpiresAt { get; set; }
        public int SessionTimeoutMinutes { get; set; }
        public bool RequiresOtp { get; set; }
        public string? PendingPersonnelNumber { get; set; }
        public DateTime? OtpExpiresAt { get; set; }
        public string? DeliveryHint { get; set; }
    }

    public class UserInfoDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PersonnelNumber { get; set; } = string.Empty;
        public string? Rank { get; set; }
        public string? EmailAddress { get; set; }
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? StationName { get; set; }
        public bool IsActivated { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();
        public string FullName => $"{FirstName} {LastName}";
    }
}
