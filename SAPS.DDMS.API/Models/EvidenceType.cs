using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models
{
    public class EvidenceType
    {
        [Key]
        public int EvidenceTypeID { get; set; }

        [Required]
        [MaxLength(20)]
        public string? Name { get; set; }

        public virtual ICollection<Evidence>? Evidences { get; set; }

    }
}
