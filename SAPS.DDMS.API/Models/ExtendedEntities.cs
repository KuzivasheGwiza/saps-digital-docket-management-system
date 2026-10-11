using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    // Docket status history for immutable transition records
    public class DocketStatusHistory
    {
        [Key]
        public int HistoryID { get; set; }

        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket? Docket { get; set; }

        [MaxLength(50)]
        public string? FromStatus { get; set; }

        [MaxLength(50)]
        public string? ToStatus { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        public int? ChangedByUserID { get; set; }
        public string? Reason { get; set; }
    }

    // Evidence chain of custody transfer record
    public class EvidenceTransfer
    {
        [Key]
        public int TransferID { get; set; }

        [ForeignKey("Evidence")]
        public int FK_EvidenceID { get; set; }
        public virtual Evidence? Evidence { get; set; }

        [ForeignKey("FromLocation")]
        public int FromLocationID { get; set; }
        public virtual EvidenceLocation? FromLocation { get; set; }

        [ForeignKey("ToLocation")]
        public int ToLocationID { get; set; }
        public virtual EvidenceLocation? ToLocation { get; set; }

        public DateTime TransferredAt { get; set; } = DateTime.UtcNow;

        public int TransferredByUserID { get; set; }

        public int ReceivingOfficerUserID { get; set; }

        [MaxLength(50)]
        public string? SignatureMethod { get; set; }

        [MaxLength(256)]
        public string? PayloadHash { get; set; }

        [MaxLength(500)]
        public string? TransferReason { get; set; }
    }

    // Prosecutor bundle metadata and deterministic manifest item
    public class ProsecutorBundle
    {
        [Key]
        public int BundleID { get; set; }

        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket? Docket { get; set; }

        [MaxLength(256)]
        public string? Title { get; set; }

        public string? Notes { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Draft";

        public string? AISummaryText { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedByUserID { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedByUserID { get; set; }
        public DateTime? ExportedAt { get; set; }
        public int? ExportedByUserID { get; set; }
        public string? ManifestPath { get; set; }
        public int Version { get; set; } = 1;

        public int? RecipientProsecutorUserID { get; set; }
        public DateTime? SharedAt { get; set; }
        public int? SharedByUserID { get; set; }

        [MaxLength(500)]
        public string? ShareNote { get; set; }

        [MaxLength(128)]
        public string? SharedManifestHash { get; set; }

        public virtual ICollection<ProsecutorBundleItem>? Manifest { get; set; }
        public virtual ICollection<ProsecutorBundleRevision>? Revisions { get; set; }
    }

    public class ProsecutorBundleItem
    {
        [Key]
        public int ItemID { get; set; }

        [ForeignKey("ProsecutorBundle")]
        public int FK_BundleID { get; set; }
        public virtual ProsecutorBundle? ProsecutorBundle { get; set; }

        [MaxLength(50)]
        public string? Section { get; set; }   // e.g., "A-clip"

        public int Order { get; set; }

        [MaxLength(256)]
        public string? Name { get; set; }

        public bool IsPrivate { get; set; } = false;
        public string? PrivacyReason { get; set; }
    }

    public class ProsecutorBundleRevision
    {
        [Key]
        public int RevisionID { get; set; }

        [ForeignKey(nameof(ProsecutorBundle))]
        public int FK_BundleID { get; set; }
        public virtual ProsecutorBundle? ProsecutorBundle { get; set; }

        public int Version { get; set; }

        [MaxLength(256)]
        public string? Title { get; set; }
        public string? Notes { get; set; }
        public string? AISummaryText { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Draft";
        public string ManifestJson { get; set; } = "[]";
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedByUserID { get; set; }
    }

    // Privacy flag history for chronological records
    public class PrivacyFlagHistory
    {
        [Key]
        public int PrivacyFlagHistoryID { get; set; }

        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket? Docket { get; set; }

        public bool IsPrivate { get; set; }
        public string? Reason { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int ChangedByUserID { get; set; }
    }

    // Minimal AI logs + extraction/suggestion stubs
    public class AIInteractionLog
    {
        [Key]
        public int AIInteractionLogID { get; set; }

        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket? Docket { get; set; }

        [MaxLength(50)]
        public string? ActionType { get; set; }

        public string? Payload { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int PerformedByUserID { get; set; }
    }

    public class AIExtraction
    {
        [Key]
        public int AIExtractionID { get; set; }

        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket? Docket { get; set; }

        public string? ExtractedData { get; set; }
        public bool IsVerified { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AISuggestion
    {
        [Key]
        public int AISuggestionID { get; set; }

        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket? Docket { get; set; }

        public string? SuggestionType { get; set; }
        public string? Content { get; set; }
        public double Score { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // Court outcome
    public class CourtOutcome
    {
        [Key]
        public int CourtOutcomeID { get; set; }

        [ForeignKey("Docket")]
        public int FK_DocketID { get; set; }
        public virtual Docket? Docket { get; set; }

        [MaxLength(50)]
        public string? Outcome { get; set; } // Convicted | Acquitted | ...
        public DateTime OutcomeDate { get; set; }
        public string? Sentence { get; set; }
    }

    // Person archive record
    public class PersonArchive
    {
        [Key]
        public int PersonArchiveID { get; set; }

        [ForeignKey("Person")]
        public int FK_PersonID { get; set; }
        public virtual Person? Person { get; set; }

        public string? Reason { get; set; }
        public int ArchivedByUserID { get; set; }
        public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    }

    public class UserRoleHistory
    {
        [Key]
        public int UserRoleHistoryID { get; set; }

        [ForeignKey("User")]
        public int FK_UserID { get; set; }
        public virtual User? User { get; set; }

        public int OldRoleID { get; set; }
        public int NewRoleID { get; set; }

        [MaxLength(100)]
        public string? OldRoleName { get; set; }

        [MaxLength(100)]
        public string? NewRoleName { get; set; }

        public string? Reason { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public int ChangedByUserID { get; set; }
    }

    public class Faq
    {
        [Key]
        public int FaqID { get; set; }

        [Required]
        [MaxLength(250)]
        public string Question { get; set; } = string.Empty;

        [Required]
        public string Answer { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedByUserID { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedByUserID { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedByUserID { get; set; }
        public bool IsPublished { get; set; } = false;
        public DateTime? PublishedAt { get; set; }
        public int? PublishedByUserID { get; set; }
    }

    public class InvestigationNoteArchiveRequest
    {
        [Key]
        public int ArchiveRequestID { get; set; }

        [ForeignKey("InvestigationNote")]
        public int FK_NoteID { get; set; }
        public virtual InvestigationNote? InvestigationNote { get; set; }

        public string Reason { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public int RequestedByUserID { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByUserID { get; set; }
        public string? ReviewNotes { get; set; }
    }
}
