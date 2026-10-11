using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class SessionLog
    {
        [Key]
        public int SessionLogID { get; set; }
        [MaxLength(256)]
        public string ActionType { get; set; }

        [MaxLength(256)]
        public string AffectedEntity { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int? AffectedRecordID { get; set; }


        [ForeignKey("Session")]
        public int FK_SessionID { get; set; }
        public virtual Session Session { get; set; }
    }
}
