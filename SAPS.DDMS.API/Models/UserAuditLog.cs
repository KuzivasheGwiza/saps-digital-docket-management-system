using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models
{
    public class UserAuditLog
    {
        [Key]
        public int UserAuditLogID { get; set; }

        [Required]
        [MaxLength(50)]
        public string? ActionType { get; set; }

        [Required]
        [MaxLength(50)]
        public string? AffectedEntity { get; set; }

        public int? AffectedEntityID { get; set; }

        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Reason { get; set; }
        public string? IPAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ForeignKey("User")]
        public int FK_UserID { get; set; }
        public virtual User User { get; set; } = null!;
    }
}
