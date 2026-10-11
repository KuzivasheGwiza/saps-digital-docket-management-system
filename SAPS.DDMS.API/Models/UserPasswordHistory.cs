using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class UserPasswordHistory
    {
        [Key]
        public int UserPasswordHistoryID { get; set; }

        [ForeignKey(nameof(User))]
        public int FK_UserID { get; set; }
        public virtual User? User { get; set; }

        [Required]
        [MaxLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(50)]
        public string ChangeType { get; set; } = "PasswordChange";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
