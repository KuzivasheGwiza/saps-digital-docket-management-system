using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models
{
    public class Incident
    {
        [Key]
        public int IncidentID { get; set; }

        [Required]
        public DateTime OccurrenceDate { get; set; }

        [MaxLength(256)]
        public string LocationType { get; set; }

        [MaxLength(256)]
        public string Description { get; set; }

        [ForeignKey("GeoLocation")]
        public int? FK_GeoID { get; set; }
        public virtual GeoLocation GeoLocation { get; set; }

        [ForeignKey("IncidentType")]
        public int? FK_IncidentTypeID { get; set; }
        public virtual IncidentType IncidentType { get; set; }

        [MaxLength(100)]
        public string? CustomIncidentType { get; set; }

        [ForeignKey("User")]
        public int? FK_UserID { get; set; }
        public virtual User User { get; set; }

        public virtual Docket Docket { get; set; }
    }
}
