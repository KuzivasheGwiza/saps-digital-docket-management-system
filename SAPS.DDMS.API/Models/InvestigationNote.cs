using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class InvestigationNote
    {
        [Key]
        public int NoteID { get; set; }

        public DateTime EntryDate { get; set; } = DateTime.UtcNow;

      

        [Required]
        [MaxLength(256)]
        public string Description { get; set; } 

        [MaxLength(50)]
        public string Status { get; set; } = "Active"; // Active | ArchivePending | Archived | Approved | Closed

        [ForeignKey("Investigation")]
        public int FK_InvestigationID { get; set; }
        public virtual Investigation Investigation { get; set; }

        [ForeignKey("Author")]
        public int FK_AuthorUserID { get; set; }  
        public virtual User Author { get; set; }  

        public virtual ICollection<NoteVersion> NoteVersions { get; set; }
        public virtual ICollection<NoteAttachment> NoteAttachments { get; set; }

    }
}
