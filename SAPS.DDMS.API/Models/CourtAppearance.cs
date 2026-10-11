using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    [Table("CourtAppearance")]
    public class CourtAppearance
    {
        [Key]
        public int CourtAppearanceID { get; set; }

        [Required]
        public DateTime AppearanceDate { get; set; }

        [MaxLength(256)]
        public string CourtroomName { get; set; } = string.Empty;

        [MaxLength(256)]
        public string PresidingOfficer { get; set; } = string.Empty;

        [MaxLength(256)]
        public string AppearanceReason { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? Outcome { get; set; }

        [MaxLength(2000)]
        public string? Sentence { get; set; }

        public DateTime? RemandDate { get; set; }

        [MaxLength(500)]
        public string? TypescriptLink { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public int FK_DocketID { get; set; }

        [ForeignKey(nameof(FK_DocketID))]
        public virtual Docket? Docket { get; set; }
    }
}
