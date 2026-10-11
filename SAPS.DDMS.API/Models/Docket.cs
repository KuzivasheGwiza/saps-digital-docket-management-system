using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models
{
    public class Docket
    {
        [Key]
        public int DocketID { get; set; }

        [Required]
        [MaxLength(256)]
        public string? DocketNumber { get; set; }

        [Required]
        [MaxLength(50)]
        public string? OBReferenceNumber { get; set; }

        [MaxLength(50)]
        public string? FICReferenceNumber { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public DateTime? DateAllocated { get; set; }
        public DateTime? DocketArchivedAt { get; set; }
        public DateTime? DocketClosedAt { get; set; }
        public DateTime? ReopenedAt { get; set; }

        [MaxLength(256)]
        public string? PriorityLevel { get; set; }

        [MaxLength(30)]
        public string SensitivityLevel { get; set; } = "NORMAL";

        public DateTime IncidentDate { get; set; }

        [MaxLength(256)]
        public string? ModusOperandi { get; set; }

        public string? DocketSummary { get; set; }

        [MaxLength(256)]
        public string? ComplainantFullName { get; set; }

        [MaxLength(30)]
        public string? ComplainantContactNumber { get; set; }

        [MaxLength(50)]
        public string? CaseType { get; set; }

        [ForeignKey("Incident")]
        public int? FK_IncidentID { get; set; }
        public virtual Incident Incident { get; set; } = null!;

        [ForeignKey("DocketStatus")]
        public int FK_DocketStatusID { get; set; }
        public virtual DocketStatus DocketStatus { get; set; } = null!;


        [ForeignKey("Station")]
        public int FK_StationID { get; set; }
        public virtual Station Station { get; set; } = null!;

        [ForeignKey("CreatedByUser")]
        public int FK_CreatedByUserID { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        [ForeignKey("Investigation")]
        public int? FK_InvestigationID { get; set; }
        public virtual Investigation Investigation { get; set; } = null!;
        public virtual ICollection<Evidence> Evidences { get; set; } = null!;
     
        public virtual ICollection<DocketPersonLink> DocketPersonLinks { get; set; } = null!;
        public virtual ICollection<DocketTimer> DocketTimers { get; set; } = null!;
        public virtual ICollection<DocketPrivacyFlag> DocketPrivacyFlags { get; set; } = null!;

        // New navigation for status history
        public virtual ICollection<DocketStatusHistory> DocketStatusHistories { get; set; } = new List<DocketStatusHistory>();

    }
}
