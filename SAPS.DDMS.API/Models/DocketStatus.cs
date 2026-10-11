using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models
{
    public class DocketStatus
    {
        [Key]
        public int DocketStatusID { get; set; }

        [Required]
        [MaxLength(50)]
        public string? StatusName { get; set; }

        public virtual ICollection<Docket> Dockets { get; set; } = new List<Docket>();
    }
}

