using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models
{
    public class Permissions
    {
        [Key]
        public int PermissionID { get; set; }
        [Required]
        [MaxLength(256)]
        public string? Name { get; set; }


    }
}
