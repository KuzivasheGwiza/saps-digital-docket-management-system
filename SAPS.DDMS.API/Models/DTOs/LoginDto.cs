using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "Personnel number or email address is required.")]
    [StringLength(100, ErrorMessage = "Personnel number or email address cannot exceed 100 characters.")]
    public string PersonnelNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class VerifyOtpDto
{
    [Required(ErrorMessage = "Personnel number or email address is required.")]
    [StringLength(100, ErrorMessage = "Personnel number or email address cannot exceed 100 characters.")]
    public string PersonnelNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "One-time PIN is required.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "One-time PIN must be 6 digits.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "One-time PIN must contain six digits.")]
    public string OtpCode { get; set; } = string.Empty;
}
