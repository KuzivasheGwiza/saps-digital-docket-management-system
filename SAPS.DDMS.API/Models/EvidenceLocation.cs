using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models
{
    public class EvidenceLocation
    {
        [Key]
        public int EvidenceLocationID { get; set; }

        [Required]
        [MaxLength(60)]
        public string? LocationName { get; set; }

        [MaxLength(100)]
        public string? PhysicalAddress { get; set; }

        [MaxLength(256)]
        public string StorageRequirements { get; set; }

        public virtual ICollection<Evidence> Evidences { get; set; }
    }
        
}
