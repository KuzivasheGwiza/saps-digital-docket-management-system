namespace SAPS.DDMS.API.Models.DTOs
{
    public class DocketResponseDto
    {
        public int DocketID { get; set; }
        public int? InvestigationID { get; set; }
        public string DocketNumber { get; set; } = string.Empty;
        public string OBReferenceNumber { get; set; } = string.Empty;
        public string FICReferenceNumber { get; set; } = string.Empty;
        public string DocketSummary { get; set; } = string.Empty;
        public string ModusOperandi { get; set; } = string.Empty;
        public string PriorityLevel { get; set; } = string.Empty;
        public string SensitivityLevel { get; set; } = "NORMAL";
        public string CaseType { get; set; } = string.Empty;
        public string ComplainantFullName { get; set; } = string.Empty;
        public string ComplainantContactNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
        public DateTime? DateAllocated { get; set; }
        public DateTime? AssignmentAcceptedAt { get; set; }
        public DateTime? InspectionDueAt { get; set; }
        public DateTime? InspectionCompletedAt { get; set; }
        public string AllocationReviewStatus { get; set; } = string.Empty;
        public string AllocationReviewNotes { get; set; } = string.Empty;
        public DateTime? DocketArchivedAt { get; set; }
        public DateTime? DocketClosedAt { get; set; }
        public DateTime? ReopenedAt { get; set; }
        public string CrimeCategory { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public string Jurisdiction { get; set; } = string.Empty;
        public string SuspectName { get; set; } = string.Empty;
        public bool PrivacyFlag { get; set; }
        public int? AssignedOfficerUserID { get; set; }
        public string AssignedOfficerName { get; set; } = string.Empty;
        public int CreatedByUserID { get; set; }
        public IncidentResponseDto? Incident { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
    }
}
