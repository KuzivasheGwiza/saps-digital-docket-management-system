using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAPS.DDMS.API.Models
{
    public class SystemSetting
    {
        [Key]
        public int SettingID { get; set; }

        [Required]
        [MaxLength(100)]
        public string SettingKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string SettingValue { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(30)]
        public string DataType { get; set; } = "int";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? UpdatedByUserID { get; set; }
    }

    public class BackupRecord
    {
        [Key]
        public int BackupID { get; set; }

        [Required]
        [MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedByUserID { get; set; }
        public long SizeBytes { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Complete";

        public DateTime? RestoredAt { get; set; }
        public int? RestoredByUserID { get; set; }
        public string? Notes { get; set; }

        [NotMapped]
        public decimal SizeMB => Math.Round(SizeBytes / 1024m / 1024m, 2);
    }

    public class SmsMessage
    {
        [Key]
        public int SmsMessageID { get; set; }

        public int? FK_UserID { get; set; }

        [MaxLength(30)]
        public string RecipientNumber { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Purpose { get; set; } = "Notification";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SentAt { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Generated";

        [MaxLength(100)]
        public string? ProviderReference { get; set; }
    }
}
