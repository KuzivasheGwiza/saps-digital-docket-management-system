using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models
{
    public class Evidence
    {
        [Key]
        public int EvidenceID { get; set; }

        [Required]
        public string? Description { get; set; }

        public int Quantity { get; set; }

        public DateTime DateCollected { get; set; }

        [MaxLength(256)]
        public string? CollectionMethod { get; set; }

        [MaxLength(256)]
        public string? StateOfItem { get; set; }

        [Required]
        [MaxLength(50)]
        public string? EvidenceReferenceNumber { get; set; }

        public string? ImagePath { get; set; }
        public string? FileHash { get; set; }

        [MaxLength(50)]
        public string? SignatureMethod { get; set; }    // "Ink" | "Stylus" | "Smartcard"
        [MaxLength(256)]
        public string? PayloadHash { get; set; }

        [ForeignKey("EvidenceType")]
        public int FK_EvidenceTypeID { get; set; }
        public virtual EvidenceType? EvidenceType { get; set; }

        [ForeignKey("EvidenceLocation")]
        public int FK_EvidenceLocationID { get; set; }
        public virtual EvidenceLocation? EvidenceLocation { get; set; }

        [ForeignKey("Investigation")]
        public int FK_InvestigationID { get; set; }
        public virtual Investigation? Investigation { get; set; }

        [ForeignKey("User")]
        public int FK_UserID { get; set; }
        public virtual User? User { get; set; }

        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket? Docket { get; set; }
    }
}
