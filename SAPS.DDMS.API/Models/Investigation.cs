using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class Investigation
    {
        [Key]
        public int InvestigationID { get; set; }

        [Required]
        public string Title { get; set; }

        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }

        [ForeignKey("InvestigationStatus")]
        public int FK_InvestigationStatusID { get; set; }
        public virtual InvestigationStatus InvestigationStatus { get; set; }

        public virtual Docket Docket { get; set; }
        public virtual ICollection<InvestigationNote> InvestigationNotes { get; set; }
        public virtual ICollection<InvestigationAssignment> InvestigationAssignments { get; set; }
        public virtual ICollection<Evidence> Evidences { get; set; }

    }
}
