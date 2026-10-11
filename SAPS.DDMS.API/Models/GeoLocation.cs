using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models
{
    public class GeoLocation
    {
    
            [Key]
            public int GeoID { get; set; }

            [MaxLength(256)]
            public string? Latitude { get; set; }

            [MaxLength(256)]
            public string? Longitude { get; set; }

            [MaxLength(256)]
            public string? AreaName { get; set; }

            public virtual ICollection<Incident> Incidents { get; set; } = new HashSet<Incident>();
        }
    }
