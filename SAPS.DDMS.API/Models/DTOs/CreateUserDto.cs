using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models.DTOs;

public class CreateUserDto
{
    [Required(ErrorMessage = "First Name is required")]
    [MaxLength(50)]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Last Name is required")]
    [MaxLength(50)]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Rank is required")]
    public string? Rank { get; set; }

    [Required(ErrorMessage = "Station is required")]
    public int StationID { get; set; }

    [Required(ErrorMessage = "Email Address is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? EmailAddress { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Role is required")]
    public int RoleID { get; set; }
}
