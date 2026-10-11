using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class SessionTimer
    {
        [Key]
        public int SessionTimerID { get; set; }

        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public double? RemainingTime { get; set; }

        [ForeignKey("Session")]
        public int FK_SessionID { get; set; }
        public virtual Session? Session { get; set; }
    }
}
