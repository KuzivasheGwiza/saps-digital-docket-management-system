using SAPS.DDMS.API.Models;
// Models/NoteAttachment.cs - ADD missing properties
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class NoteAttachment
    {
        [Key]
        public int AttachmentID { get; set; }

        [MaxLength(255)]
        public string FileName { get; set; }

        [MaxLength(50)]
        public string? FileType { get; set; }

        public string? FilePath { get; set; }  

        public long FileSize { get; set; }   

        [MaxLength(256)]
        public string? FileSizeBytes { get; set; }  

        [MaxLength(64)]
        public string? Hash { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("Note")]
        public int FK_NoteID { get; set; }
        public virtual InvestigationNote? Note { get; set; }

        [ForeignKey("User")]
        public int FK_UserID { get; set; }
        public virtual User? User { get; set; }
    }
}