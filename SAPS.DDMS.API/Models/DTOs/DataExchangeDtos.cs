using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SAPS.DDMS.API.Models.DTOs
{
    public sealed class DataExchangeFilterDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? StationId { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        public int? RoleId { get; set; }
        public bool? IsActive { get; set; }
        public int? DocketId { get; set; }
        public int? UserId { get; set; }

        [MaxLength(50)]
        public string? ActionType { get; set; }
    }

    public sealed class LookupImportDocumentDto
    {
        [Range(1, 1)]
        public int SchemaVersion { get; set; } = 1;

        [Required, MaxLength(50)]
        public string LookupType { get; set; } = string.Empty;

        [Required, MinLength(1), MaxLength(500)]
        public List<LookupImportRecordDto> Records { get; set; } = new();

        public string? PreviewToken { get; set; }
        public bool ConfirmApply { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalFields { get; set; }
    }

    public sealed class LookupImportRecordDto
    {
        [Required, MaxLength(256)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? PhysicalAddress { get; set; }

        [MaxLength(256)]
        public string? StorageRequirements { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalFields { get; set; }
    }

    public sealed class LookupImportPreviewDto
    {
        public int SchemaVersion { get; init; } = 1;
        public string LookupType { get; init; } = string.Empty;
        public string ContentHash { get; init; } = string.Empty;
        public int TotalRecords { get; init; }
        public int CreateCount { get; init; }
        public int UpdateCount { get; init; }
        public int UnchangedCount { get; init; }
        public bool CanApply { get; init; }
        public string? PreviewToken { get; init; }
        public DateTime? PreviewExpiresAtUtc { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
        public IReadOnlyList<LookupImportPreviewRowDto> Rows { get; init; } = Array.Empty<LookupImportPreviewRowDto>();
    }

    public sealed class LookupImportPreviewRowDto
    {
        public string Name { get; init; } = string.Empty;
        public string Action { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
    }

    public sealed class LookupImportApplyResultDto
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string LookupType { get; init; } = string.Empty;
        public int Created { get; init; }
        public int Updated { get; init; }
        public int Unchanged { get; init; }
        public string ContentHash { get; init; } = string.Empty;
    }
}
