using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class DocketPersonLink
    {
        [Key]
        public int LinkID { get; set; }

        public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(50)]
        public string RelationshipRole { get; set; } = "Unspecified";

        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket Docket { get; set; } = null!;

        [ForeignKey("Person")]
        public int FK_PersonID { get; set; }
        public virtual Person Person { get; set; } = null!;

        [ForeignKey("LinkedByUser")]
        public int FK_LinkedByUserID { get; set; }
        public virtual User LinkedByUser { get; set; } = null!;
    }   
}
