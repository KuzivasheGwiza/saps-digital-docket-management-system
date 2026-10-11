using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models
{
    public class NoteVersion
    {
        [Key]
        public int VersionID { get; set; }

        public int VersionNumber { get; set; }

        [MaxLength(256)]
        public string NoteTextBefore { get; set; }

        [MaxLength(256)]
        public string NoteTextAfter { get; set; }

        public DateTime EditedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(256)]
        public string Reason { get; set; }

        [ForeignKey("Note")]
        public int FK_NoteID { get; set; }
        public virtual InvestigationNote Note { get; set; }

        [ForeignKey("User")]
        public int FK_UserID { get; set; }
        public virtual User User { get; set; }
    }
}
