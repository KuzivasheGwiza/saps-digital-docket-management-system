using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Repositories;
using SAPS.DDMS.API.Services;

using static SAPS.DDMS.API.Models.DTOs.DashboardDtos;

namespace SAPS.DDMS.API.Services
{
    public interface IDashboardService
    {
        Task<ServiceResult<DashboardStatsDto>> GetDashboardStatsAsync(int userId);
    }

    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserRepository _userRepository;

        public DashboardService(ApplicationDbContext context, IUserRepository userRepository)
        {
            _context = context;
            _userRepository = userRepository;
        }

        public async Task<ServiceResult<DashboardStatsDto>> GetDashboardStatsAsync(int userId)
        {
            var userContext = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserID == userId)
                .Select(u => new { StationId = (int?)u.FK_StationID, RoleName = u.Role.Name })
                .FirstOrDefaultAsync();

            if (userContext == null || !userContext.StationId.HasValue)
                return ServiceResult<DashboardStatsDto>.Failure("User not found.");

            if (userContext.RoleName == "System Administrator")
                return ServiceResult<DashboardStatsDto>.Failure(
                    "Docket dashboard information is unavailable because the System Administrator role does not perform docket operations.");

            var stationId = userContext.StationId;

            var docketQuery = _context.Dockets
                .AsNoTracking()
                .Where(d => d.FK_StationID == stationId.Value);

            // Keep the dashboard lightweight: aggregate and project in SQL instead of
            // materialising every docket and its related collections.
            var statusDistribution = await docketQuery
                .GroupBy(d => d.DocketStatus.StatusName)
                .Select(g => new StatusCountDto
                {
                    Status = g.Key ?? "Unknown",
                    Count = g.Count()
                })
                .ToListAsync();

            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var monthlyTrends = await docketQuery
                .Where(d => d.DateCreated >= sixMonthsAgo)
                .GroupBy(d => new { d.DateCreated.Year, d.DateCreated.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new MonthlyTrendDto
                {
                    Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Count = g.Count()
                })
                .ToListAsync();

            var inspectionAssignments = await _context.InvestigationAssignments
                .AsNoTracking()
                .Where(a => a.Investigation != null &&
                    a.Investigation.Docket.FK_StationID == stationId.Value &&
                    (a.DateAccepted.HasValue || a.InspectionDueAt.HasValue))
                .Select(a => new { a.InspectionDueAt, a.InspectionCompletedAt })
                .ToListAsync();

            var inspectionCompleted = inspectionAssignments.Count(a => a.InspectionCompletedAt.HasValue);
            var inspectionOverdue = inspectionAssignments.Count(a =>
                !a.InspectionCompletedAt.HasValue &&
                a.InspectionDueAt.HasValue &&
                a.InspectionDueAt.Value < DateTime.UtcNow);
            var inspectionPending = Math.Max(inspectionAssignments.Count - inspectionCompleted - inspectionOverdue, 0);

            var recentDockets = await docketQuery
                .OrderByDescending(d => d.DateCreated)
                .Take(5)
                .Select(d => new DashboardDocketDto
                {
                    DocketID = d.DocketID,
                    DocketNumber = d.DocketNumber ?? string.Empty,
                    OBReferenceNumber = d.OBReferenceNumber ?? string.Empty,
                    Status = d.DocketStatus.StatusName ?? string.Empty,
                    DateCreated = d.DateCreated,
                    AssignmentAcceptedAt = d.Investigation == null ? null : d.Investigation.InvestigationAssignments
                        .OrderByDescending(a => a.DateAssigned).Select(a => a.DateAccepted).FirstOrDefault(),
                    InspectionDueAt = d.Investigation == null ? null : d.Investigation.InvestigationAssignments
                        .OrderByDescending(a => a.DateAssigned).Select(a => a.InspectionDueAt).FirstOrDefault(),
                    InspectionCompletedAt = d.Investigation == null ? null : d.Investigation.InvestigationAssignments
                        .OrderByDescending(a => a.DateAssigned).Select(a => a.InspectionCompletedAt).FirstOrDefault()
                })
                .ToListAsync();

            var inspectionDockets = await docketQuery
                .Where(d => d.DocketStatus.StatusName == "ALLOCATED" ||
                    (d.Investigation != null && d.Investigation.InvestigationAssignments.Any(a =>
                        a.DateAccepted.HasValue || a.InspectionDueAt.HasValue)))
                .OrderByDescending(d => d.DateAllocated ?? d.DateCreated)
                .Take(6)
                .Select(d => new DashboardDocketDto
                {
                    DocketID = d.DocketID,
                    DocketNumber = d.DocketNumber ?? string.Empty,
                    OBReferenceNumber = d.OBReferenceNumber ?? string.Empty,
                    Status = d.DocketStatus.StatusName ?? string.Empty,
                    DateCreated = d.DateCreated,
                    AssignmentAcceptedAt = d.Investigation == null ? null : d.Investigation.InvestigationAssignments
                        .OrderByDescending(a => a.DateAssigned).Select(a => a.DateAccepted).FirstOrDefault(),
                    InspectionDueAt = d.Investigation == null ? null : d.Investigation.InvestigationAssignments
                        .OrderByDescending(a => a.DateAssigned).Select(a => a.InspectionDueAt).FirstOrDefault(),
                    InspectionCompletedAt = d.Investigation == null ? null : d.Investigation.InvestigationAssignments
                        .OrderByDescending(a => a.DateAssigned).Select(a => a.InspectionCompletedAt).FirstOrDefault()
                })
                .ToListAsync();

            var officerWorkload = await _context.InvestigationAssignments
                .AsNoTracking()
                .Where(ia => ia.User != null && ia.User.FK_StationID == stationId.Value)
                .GroupBy(ia => new { ia.User!.UserID, ia.User.FirstName, ia.User.LastName })
                .Select(g => new OfficerWorkloadDto
                {
                    OfficerName = g.Key.FirstName + " " + g.Key.LastName,
                    AssignedCases = g.Count(),
                    CompletedCases = g.Count(a => a.Investigation != null &&
                        a.Investigation.Docket.DocketStatus.StatusName == "CLOSED")
                })
                .ToListAsync();

            var stats = new DashboardStatsDto
            {
                TotalDockets = statusDistribution.Sum(s => s.Count),
                OpenDockets = statusDistribution.FirstOrDefault(s => s.Status == "OPEN")?.Count ?? 0,
                AllocatedDockets = statusDistribution.FirstOrDefault(s => s.Status == "ALLOCATED")?.Count ?? 0,
                UnderInvestigation = statusDistribution.FirstOrDefault(s => s.Status == "UNDER_INVESTIGATION")?.Count ?? 0,
                ClosedDockets = statusDistribution.FirstOrDefault(s => s.Status == "CLOSED")?.Count ?? 0,
                InspectionCompleted = inspectionCompleted,
                InspectionPending = inspectionPending,
                InspectionOverdue = inspectionOverdue,
                InspectionCompliancePercent = inspectionAssignments.Count == 0
                    ? 0
                    : (int)Math.Round((double)inspectionCompleted / inspectionAssignments.Count * 100),
                StatusDistribution = statusDistribution,
                MonthlyTrends = monthlyTrends,
                OfficerWorkload = officerWorkload,
                RecentDockets = recentDockets,
                InspectionDockets = inspectionDockets
            };

            return ServiceResult<DashboardStatsDto>.SuccessResult(stats);
        }
    }
}
