using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SAPS.DDMS.API.Models;


namespace SAPS.DDMS.API.Models
{
    public class DocketPrivacyFlag
    {
        [Key]
        public int FlagID { get; set; }

        [MaxLength(256)]
        public string? FieldName { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RemovedAt { get; set; }

        [MaxLength(500)]
        public string? RemovalReason { get; set; }

        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket Docket { get; set; } = null!;

        [ForeignKey("AppliedByUser")]
        public int FK_Applied_By_UserID { get; set; }
        public virtual User AppliedByUser { get; set; } = null!;

        [ForeignKey("AppliedForUser")]
        public int FK_Applied_For_UserID { get; set; }
        public virtual User AppliedForUser { get; set; } = null!;

        [ForeignKey("RemovedByUser")]
        public int? FK_Removed_By_UserID { get; set; }
        public virtual User RemovedByUser { get; set; } = null!;

        [ForeignKey("RemovedForUser")]
        public int? FK_Removed_For_UserID { get; set; }
        public virtual User RemovedForUser { get; set; } = null!;    
    }
}
