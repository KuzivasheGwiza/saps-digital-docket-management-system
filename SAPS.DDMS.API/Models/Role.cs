using System.ComponentModel.DataAnnotations;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models
{
    public class Role
    {
        [Key]
        public int RoleID { get; set; }

        [Required]
        [MaxLength(256)]
        public string? Name { get; set; }

        [MaxLength(256)]
        public string? Description { get; set; }

        public string? Permissions { get; set; }

        public virtual ICollection<User> Users { get; set; }
        public virtual ICollection<RolePermissions> RolePermissions { get; set; }
    }
}