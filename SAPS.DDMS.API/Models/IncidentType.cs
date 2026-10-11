using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models
{
    public class IncidentType
    {
        [Key]
        public int IncidentTypeID { get; set; }

        [Required]
        [MaxLength(256)]
        public string TypeName { get; set; }

        [MaxLength(256)]
        public string Description { get; set; }

        public virtual ICollection<Incident> Incidents { get; set; }
    }
}

