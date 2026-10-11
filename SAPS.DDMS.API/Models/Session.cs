
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models
{
    public class Session
    {
        [Key]
        public int SessionID { get; set; }

        public DateTime Login { get; set; } = DateTime.UtcNow;
        public DateTime? LastActivity { get; set; }
        public DateTime? Logout { get; set; }
        public DateTime ExpiryTime { get; set; }

        [MaxLength(256)]
        public string TokenHash { get; set; }

        [ForeignKey("User")]
        public int FK_UserID { get; set; }
        public virtual User User { get; set; }

        public virtual ICollection<SessionLog> SessionLogs { get; set; }
        public virtual ICollection<SessionTimer> SessionTimers { get; set; }
    }
}
