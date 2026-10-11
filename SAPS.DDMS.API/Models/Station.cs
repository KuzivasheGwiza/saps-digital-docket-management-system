using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Org.BouncyCastle.Bcpg.OpenPgp;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models;

[Table("Stations")]
public class Station
{
    [Key]
    
    public int StationID { get; set; }

    [Required]
    [MaxLength(256)]
    public string StationName { get; set; } = string.Empty;
    public string? Code { get; set; }

    [MaxLength(50)]
    public string? Province { get; set; }


    [MaxLength(50)]
    public string? Region { get; set; }

    public virtual ICollection<User> Users { get; set; }
    public virtual ICollection<Docket> Dockets { get; set; } = new List<Docket>();
}