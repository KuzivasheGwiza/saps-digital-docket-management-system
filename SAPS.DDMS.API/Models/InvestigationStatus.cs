using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models
{
    public class InvestigationStatus
    {
        [Key]
        public int InvestigationStatusID { get; set; }

        [Required]
        [MaxLength(50)]
        public string StatusName { get; set; }

        public virtual ICollection<Investigation> Investigations { get; set; }
    }
}
