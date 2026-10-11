using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models.DTOs;

public class UpdateUserDto
{
    [Required]
    [MaxLength(50)]
    public string? FirstName { get; set; }

    [Required]
    [MaxLength(50)]
    public string? LastName { get; set; }

    [Required]
    public string? Rank { get; set; }

    [Required]
    public int StationID { get; set; }

    [Required]
    [EmailAddress]
    public string? EmailAddress { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    [Required]
    public int RoleID { get; set; }

    public string? Reason { get; set; }
}
