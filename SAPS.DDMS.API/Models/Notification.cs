using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models
{
    public class Notification
    {
        [Key]
        public int NotificationID { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; }

        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
        public bool IsEmailSent { get; set; } = false;
        public bool IsSMSSent { get; set; } = false;

        [MaxLength(20)]
        public string PriorityLevel { get; set; }

        [ForeignKey("User")]
        public int FK_UserID { get; set; }
        public virtual User User { get; set; }
    }
}
