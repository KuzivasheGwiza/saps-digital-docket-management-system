using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models.DTOs;

public class ActivateAccountDto
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(12, ErrorMessage = "Password must be at least 12 characters.")]
    public string Password { get; set; } = string.Empty;
}