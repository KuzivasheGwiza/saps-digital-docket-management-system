namespace SAPS.DDMS.API.Models.DTOs
{
    public class DashboardDtos
    {
        public class DashboardStatsDto
        {
            public int TotalDockets { get; set; }
            public int OpenDockets { get; set; }
            public int AllocatedDockets { get; set; }
            public int UnderInvestigation { get; set; }
            public int ClosedDockets { get; set; }
            public int InspectionCompleted { get; set; }
            public int InspectionPending { get; set; }
            public int InspectionOverdue { get; set; }
            public int InspectionCompliancePercent { get; set; }
            public List<StatusCountDto> StatusDistribution { get; set; } = new();
            public List<MonthlyTrendDto> MonthlyTrends { get; set; } = new();
            public List<OfficerWorkloadDto> OfficerWorkload { get; set; } = new();
            public List<DashboardDocketDto> RecentDockets { get; set; } = new();
            public List<DashboardDocketDto> InspectionDockets { get; set; } = new();
        }

        public class StatusCountDto
        {
            public string? Status { get; set; }
            public int Count { get; set; }
        }

        public class MonthlyTrendDto
        {
            public string? Month { get; set; }
            public int Count { get; set; }
        }

        public class OfficerWorkloadDto
        {
            public string? OfficerName { get; set; }
            public int AssignedCases { get; set; }
            public int CompletedCases { get; set; }
        }

        public class DashboardDocketDto
        {
            public int DocketID { get; set; }
            public string DocketNumber { get; set; } = string.Empty;
            public string OBReferenceNumber { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime DateCreated { get; set; }
            public DateTime? AssignmentAcceptedAt { get; set; }
            public DateTime? InspectionDueAt { get; set; }
            public DateTime? InspectionCompletedAt { get; set; }
        }
    }
}
