using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models
{
    public class InvestigationAssignment
    {
        [Key]
        public int AssignmentID { get; set; }

        [MaxLength(20)]
        public string? Role { get; set; }

        public DateTime DateAssigned { get; set; } = DateTime.UtcNow;

        // When assignment is accepted by the IO
        public DateTime? DateAccepted { get; set; }
        public DateTime? InspectionDueAt { get; set; }

        // Inspection completion metadata (for DSC Supervisor)
        public DateTime? InspectionCompletedAt { get; set; }
        public string? InspectionNotes { get; set; }
        public int? InspectionCompletedByUserID { get; set; }

        [MaxLength(50)]
        public string AllocationReviewStatus { get; set; } = "Pending";
        public string? AllocationReviewNotes { get; set; }
        public DateTime? AllocationReviewedAt { get; set; }
        public int? AllocationReviewedByUserID { get; set; }

        [ForeignKey("User")]
        public int FK_UserID { get; set; }
        public virtual User? User { get; set; }

        [ForeignKey("Investigation")]
        public int FK_InvestigationID { get; set; }
        public virtual Investigation? Investigation { get; set; }

        [ForeignKey("AssignedBy")]
        public int FK_AssignedByUserID { get; set; }
        public virtual User? AssignedBy { get; set; }
    }
}
