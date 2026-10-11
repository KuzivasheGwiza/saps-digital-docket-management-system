using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Models
{
    public class User
    {
        [Key]
        public int UserID { get; set; }

        [Required]
        [MaxLength(50)]
        public string? FirstName { get; set; }

        [Required]
        [MaxLength(50)]
        public string? LastName { get; set; }

        [Required]
        [MaxLength(50)]
        public string EmailAddress { get; set; } = string.Empty;
        public string? Rank { get; set; }

        [Required]
        [MaxLength(50)]
        public string? PersonnelNumber { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(50)]
        public string? CommunicationPreference { get; set; }

        [MaxLength(50)]
        public string AccountStatus { get; set; } = "Active";

        [MaxLength(256)]
        public string? NotificationPreferences { get; set; }

        [Required]
        [MaxLength(50)]
        public string? Username { get; set; }

        [MaxLength(50)]
        public string? PasswordResetCode { get; set; }

        public DateTime? PasswordResetExpiresAt { get; set; }

        [MaxLength(256)]
        public string? OtpCodeHash { get; set; }

        public DateTime? OtpExpiresAt { get; set; }
        public DateTime? OtpRequestedAt { get; set; }
        public bool IsTwoFactorEnabled { get; set; } = false;

        [Required]
        [MaxLength(256)]
        public string? PasswordHash { get; set; }

        public bool IsActivated { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LastLoginDate { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public int? CreatedByUserID { get; set; }

        [ForeignKey("Role")]
        public int FK_RoleID { get; set; }
        public virtual Role? Role { get; set; }

        [ForeignKey("Station")]
        public int FK_StationID { get; set; }
        public virtual Station Station { get; set; } = null!;

        public virtual ICollection<UserAuditLog> UserAuditLogs { get; set; } = new List<UserAuditLog>();
        public virtual ICollection<Notification> Notifications { get; set; } = new HashSet<Notification>();
        public virtual ICollection<DocketPersonLink> DocketPersonLinks { get; set; } = new List<DocketPersonLink>();
        public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
        public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();
        public virtual ICollection<InvestigationAssignment> InvestigationAssignments { get; set; } = new List<InvestigationAssignment>();
        public virtual ICollection<Docket> Dockets { get; set; } = new List<Docket>();
        public virtual ICollection<InvestigationNote> InvestigationNotes { get; set; } = new List<InvestigationNote>();
        public virtual ICollection<NoteAttachment> NoteAttachments { get; set; } = new List<NoteAttachment>();
        public virtual ICollection<NoteVersion> NoteVersions { get; set; } = new HashSet<NoteVersion>();
        public virtual ICollection<Evidence> Evidences { get; set; } = new HashSet<Evidence>();
        public virtual ICollection<UserPasswordHistory> PasswordHistory { get; set; } = new List<UserPasswordHistory>();
    }
}
