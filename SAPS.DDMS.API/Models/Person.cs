using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class Person
    {
        [Key]
        public int PersonID { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [MaxLength(13)]
        public string IDNumber { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [MaxLength(20)]
        public string PhoneNumber { get; set; }

        [MaxLength(100)]
        public string EmailAddress { get; set; }

        public string PhysicalAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
        public int? ArchivedByUserID { get; set; }
        public string? ArchiveReason { get; set; }

        [ForeignKey("CreatedByUser")]
        public int FK_CreatedByUserID { get; set; }
        public virtual User CreatedByUser { get; set; }

        [ForeignKey("PersonRole")]
        public int? FK_PersonRoleID { get; set; }
        public virtual PersonRole PersonRole { get; set; }

        public virtual ICollection<DocketPersonLink> DocketPersonLinks { get; set; }
    }
}
