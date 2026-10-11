using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class Feedback
    {
        [Key]
        public int FeedbackID { get; set; }

        [ForeignKey(nameof(User))]
        public int FK_UserID { get; set; }
        public virtual User? User { get; set; }

        [Required, MaxLength(150)]
        public string Subject { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        public string Message { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Response { get; set; }

        [Required, MaxLength(30)]
        public string Status { get; set; } = "Submitted";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }
        public int? RespondedByUserID { get; set; }
    }
}
