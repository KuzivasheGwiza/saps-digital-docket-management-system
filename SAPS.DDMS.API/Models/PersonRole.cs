using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models
{
    public class PersonRole
    {
        [Key]
        public int PersonRoleID { get; set; }

        [Required]
        [MaxLength(50)]
        public string RoleName { get; set; }

        [MaxLength(10)]
        public string Code { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(200)]
        public string Description { get; set; }
    }
}
