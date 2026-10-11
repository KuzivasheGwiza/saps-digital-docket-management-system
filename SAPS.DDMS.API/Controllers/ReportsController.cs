using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Security;
using SAPS.DDMS.API.Services;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize(Policy = PermissionCatalog.ReportsView)]
    public class ReportsController : ControllerBase
    {
        private static readonly IReadOnlyDictionary<string, string[]> ReportRolesByKey =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["users-list"] = new[] { "DSC Supervisor" },
                ["dockets-list"] = new[] { "DSC Supervisor", "Docket Administrator" },
                ["evidence-list"] = new[] { "DSC Supervisor" },
                ["station-workload"] = new[] { "DSC Supervisor" },
                ["custody-summary"] = new[] { "DSC Supervisor" },
                ["docket-criteria"] = new[] { "DSC Supervisor" },
                ["status-management"] = new[] { "DSC Supervisor" },
                ["hotspot-analysis"] = new[] { "DSC Supervisor" },
                ["clearance-rate"] = new[] { "DSC Supervisor" },
                ["crime-trend"] = new[] { "DSC Supervisor" },
                ["response-time"] = new[] { "DSC Supervisor" },
                ["case-aging"] = new[] { "DSC Supervisor" }
            };

        private readonly ApplicationDbContext _db;
        private readonly IReportChartService _chartService;
        private readonly IReportPdfService _pdfService;
        private readonly IAuditService _audit;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            ApplicationDbContext db,
            IReportChartService chartService,
            IReportPdfService pdfService,
            IAuditService audit,
            ILogger<ReportsController> logger)
        {
            _db = db;
            _chartService = chartService;
            _pdfService = pdfService;
            _audit = audit;
            _logger = logger;
        }

        [HttpGet("{key}")]
        public async Task<IActionResult> GetReport(
            string key,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? status = null,
            [FromQuery] string? crimeCategory = null,
            CancellationToken cancellationToken = default)
        {
            if (!CanAccessReport(key))
                return Forbid();

            if (HasInvalidDateRange(from, to))
                return BadRequest(new { success = false, message = "The From date must be on or before the To date." });

            var report = await BuildReportAsync(key, from, to, status, crimeCategory, cancellationToken);
            if (report == null)
                return NotFound(new { success = false, message = "Report not found." });

            return Ok(report);
        }

        [HttpGet("{key}/pdf")]
        public async Task<IActionResult> DownloadPdf(
            string key,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? status = null,
            [FromQuery] string? crimeCategory = null,
            CancellationToken cancellationToken = default)
        {
            if (!CanAccessReport(key))
                return Forbid();

            if (HasInvalidDateRange(from, to))
                return BadRequest(new { success = false, message = "The From date must be on or before the To date." });

            var report = await BuildReportAsync(key, from, to, status, crimeCategory, cancellationToken);
            if (report == null)
                return NotFound(new { success = false, message = "Report not found." });

            byte[] chartImage;
            try
            {
                chartImage = BuildChartImage(report);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chart generation failed for report {ReportKey}. Continuing without a chart.", report.Key);
                chartImage = Array.Empty<byte>();
            }

            var request = CreatePdfRequest(report, chartImage);

            byte[] pdfBytes;
            try
            {
                pdfBytes = _pdfService.BuildReportPdf(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF generation failed for report {ReportKey}.", report.Key);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = "The PDF could not be generated. Please try again."
                });
            }

            var userId = GetCurrentUserId();
            await _audit.LogActionAsync(
                "EXPORT_REPORT_PDF",
                "Report",
                null,
                null,
                new { report.Key, report.Title },
                userId,
                ipAddress: GetClientIpAddress());

            return File(
                pdfBytes,
                "application/pdf",
                BuildFileName(report));
        }

        private bool CanAccessReport(string key)
        {
            return ReportRolesByKey.TryGetValue(key.Trim(), out var allowedRoles) &&
                   allowedRoles.Any(role => User.IsInRole(role));
        }

        private async Task<ReportResult?> BuildReportAsync(string key, DateTime? from, DateTime? to, string? status, string? crimeCategory, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var requester = await _db.Users
                .AsNoTracking()
                .Include(user => user.Role)
                .Include(user => user.Station)
                .FirstOrDefaultAsync(user => user.UserID == userId, cancellationToken);

            if (requester == null)
                return null;

            var generatedBy = $"{requester.FirstName} {requester.LastName}".Trim();
            var generatedWhere = requester.Role?.Name == "System Administrator"
                ? "All Stations"
                : requester.Station?.StationName ?? "Assigned Station";
            int? stationScope = requester.Role?.Name == "System Administrator" ? null : requester.FK_StationID;

            return key.ToLowerInvariant() switch
            {
                "users-list" => await BuildUsersList(generatedBy, generatedWhere, stationScope, cancellationToken),
                "dockets-list" => await BuildDocketsList(generatedBy, generatedWhere, stationScope, cancellationToken),
                "evidence-list" => await BuildEvidenceList(generatedBy, generatedWhere, stationScope, cancellationToken),
                "station-workload" => await BuildStationWorkload(generatedBy, generatedWhere, stationScope, cancellationToken),
                "custody-summary" => await BuildCustodySummary(generatedBy, generatedWhere, stationScope, cancellationToken),
                "docket-criteria" => await BuildDocketCriteria(generatedBy, generatedWhere, stationScope, from, to, status, cancellationToken),
                "status-management" => await BuildStatusManagement(generatedBy, generatedWhere, stationScope, cancellationToken),
                "hotspot-analysis" => await BuildHotspotAnalysis(generatedBy, generatedWhere, stationScope, from, to, crimeCategory, cancellationToken),
                "clearance-rate" => await BuildClearanceRate(generatedBy, generatedWhere, stationScope, from, to, cancellationToken),
                "crime-trend" => await BuildCrimeTrend(generatedBy, generatedWhere, stationScope, from, to, cancellationToken),
                "response-time" => await BuildResponseTime(generatedBy, generatedWhere, stationScope, from, to, cancellationToken),
                "case-aging" => await BuildCaseAging(generatedBy, generatedWhere, stationScope, from, to, cancellationToken),
                _ => null
            };
        }

        private async Task<ReportResult> BuildUsersList(string generatedBy, string generatedWhere, int? stationScope, CancellationToken cancellationToken)
        {
            var query = _db.Users
                .AsNoTracking()
                .Include(user => user.Role)
                .Include(user => user.Station)
                .AsQueryable();

            if (stationScope.HasValue)
                query = query.Where(user => user.FK_StationID == stationScope.Value);

            var users = await query
                .OrderBy(user => user.LastName)
                .ThenBy(user => user.FirstName)
                .Select(user => new
                {
                    Name = user.FirstName + " " + user.LastName,
                    Personnel = user.PersonnelNumber,
                    Role = user.Role!.Name,
                    Station = user.Station.StationName,
                    Status = user.AccountStatus,
                    Active = user.IsActive
                })
                .ToListAsync(cancellationToken);

            return new ReportResult(
                "users-list",
                "Users List Report",
                "Administrative Reporting",
                generatedBy,
                generatedWhere,
                CreateCriteria(("Station Scope", generatedWhere)),
                new[] { "Name", "Personnel", "Role", "Station", "Status", "Active" },
                users.Select(user => Row(
                    ("Name", user.Name),
                    ("Personnel", user.Personnel),
                    ("Role", user.Role),
                    ("Station", user.Station),
                    ("Status", user.Status),
                    ("Active", user.Active))).ToList(),
                Row(("Total Users", users.Count), ("Active Users", users.Count(user => user.Active)), ("Stations", users.Select(user => user.Station).Distinct().Count())));
        }

        private async Task<ReportResult> BuildDocketsList(string generatedBy, string generatedWhere, int? stationScope, CancellationToken cancellationToken)
        {
            var query = _db.Dockets
                .AsNoTracking()
                .Include(docket => docket.DocketStatus)
                .Include(docket => docket.Station)
                .AsQueryable();

            if (stationScope.HasValue)
                query = query.Where(docket => docket.FK_StationID == stationScope.Value);

            var dockets = await query
                .OrderByDescending(docket => docket.DateCreated)
                .Select(docket => new
                {
                    docket.DocketNumber,
                    OB = docket.OBReferenceNumber,
                    Station = docket.Station.StationName,
                    Status = docket.DocketStatus.StatusName,
                    docket.PriorityLevel,
                    docket.DateCreated
                })
                .ToListAsync(cancellationToken);

            return new ReportResult(
                "dockets-list",
                "Dockets List Report",
                "Operational Reporting",
                generatedBy,
                generatedWhere,
                CreateCriteria(("Station Scope", generatedWhere)),
                new[] { "DocketNumber", "OB", "Station", "Status", "Priority", "DateCreated" },
                dockets.Select(docket => Row(
                    ("DocketNumber", docket.DocketNumber),
                    ("OB", docket.OB),
                    ("Station", docket.Station),
                    ("Status", docket.Status),
                    ("Priority", docket.PriorityLevel),
                    ("DateCreated", docket.DateCreated))).ToList(),
                Row(
                    ("Total Dockets", dockets.Count),
                    ("Number of Stations", dockets.Select(docket => docket.Station).Distinct().Count()),
                    ("Number of Status Categories", dockets.Select(docket => docket.Status).Distinct().Count())));
        }

        private async Task<ReportResult> BuildEvidenceList(string generatedBy, string generatedWhere, int? stationScope, CancellationToken cancellationToken)
        {
            var query = _db.Evidences
                .AsNoTracking()
                .Include(item => item.Docket)
                .Include(item => item.EvidenceType)
                .Include(item => item.EvidenceLocation)
                .Include(item => item.User)
                .AsQueryable();

            if (stationScope.HasValue)
                query = query.Where(item => item.Docket!.FK_StationID == stationScope.Value);

            var evidence = await query
                .OrderByDescending(item => item.DateCollected)
                .Select(item => new
                {
                    Reference = item.EvidenceReferenceNumber,
                    item.Description,
                    Type = item.EvidenceType!.Name,
                    Location = item.EvidenceLocation!.LocationName,
                    Docket = item.Docket!.DocketNumber,
                    ItemCount = item.Quantity,
                    UploadedBy = item.User == null ? "Unknown" : item.User.FirstName + " " + item.User.LastName,
                    PersonnelNumber = item.User == null ? "-" : item.User.PersonnelNumber,
                    item.DateCollected
                })
                .ToListAsync(cancellationToken);

            return new ReportResult(
                "evidence-list",
                "Evidence List Report",
                "Operational Reporting",
                generatedBy,
                generatedWhere,
                CreateCriteria(("Station Scope", generatedWhere)),
                new[] { "Reference", "Description", "Type", "Location", "Docket", "ItemCount", "UploadedBy", "PersonnelNumber", "DateCollected" },
                evidence.Select(item => Row(
                    ("Reference", item.Reference),
                    ("Description", item.Description),
                    ("Type", item.Type),
                    ("Location", item.Location),
                    ("Docket", item.Docket),
                    ("ItemCount", item.ItemCount),
                    ("UploadedBy", item.UploadedBy),
                    ("PersonnelNumber", item.PersonnelNumber),
                    ("DateCollected", item.DateCollected))).ToList(),
                Row(("Total Evidence Records", evidence.Count), ("Total Items", evidence.Sum(item => item.ItemCount))));
        }

        private async Task<ReportResult> BuildStationWorkload(string generatedBy, string generatedWhere, int? stationScope, CancellationToken cancellationToken)
        {
            var query = _db.Dockets
                .AsNoTracking()
                .AsQueryable();

            if (stationScope.HasValue)
                query = query.Where(docket => docket.FK_StationID == stationScope.Value);

            var data = await query
                .Select(docket => new
                {
                    Province = docket.Station.Province ?? "Unknown",
                    Station = docket.Station.StationName,
                    Status = docket.DocketStatus.StatusName
                })
                .ToListAsync(cancellationToken);

            var stationTotals = data.GroupBy(item => item.Station).ToDictionary(group => group.Key, group => group.Count());
            var rows = data
                .GroupBy(item => new { item.Province, item.Station, item.Status })
                .OrderBy(group => group.Key.Province)
                .ThenBy(group => group.Key.Station)
                .ThenBy(group => group.Key.Status)
                .Select(group => Row(
                    ("Province", group.Key.Province),
                    ("Station", group.Key.Station),
                    ("Status", group.Key.Status),
                    ("Count", group.Count()),
                    ("StationTotal", stationTotals[group.Key.Station])))
                .ToList();

            return new ReportResult(
                "station-workload",
                "Transactional Report: Station Workload by Status",
                "Transactional Reporting",
                generatedBy,
                generatedWhere,
                CreateCriteria(("Station Scope", generatedWhere)),
                new[] { "Province", "Station", "Status", "Count", "StationTotal" },
                rows,
                Row(
                    ("Grand Total", data.Count),
                    ("Number of Stations", stationTotals.Count),
                    ("Number of Provinces", data.Select(item => item.Province).Distinct().Count())),
                rows,
                chartTitle: "Station workload by status");
        }

        private async Task<ReportResult> BuildCustodySummary(string generatedBy, string generatedWhere, int? stationScope, CancellationToken cancellationToken)
        {
            var query = _db.EvidenceTransfers
                .AsNoTracking()
                .AsQueryable();

            if (stationScope.HasValue)
                query = query.Where(transfer => transfer.Evidence!.Docket!.FK_StationID == stationScope.Value);

            var transfers = await query
                .Select(transfer => new
                {
                    Docket = transfer.Evidence!.Docket!.DocketNumber,
                    Evidence = transfer.Evidence.EvidenceReferenceNumber,
                    From = transfer.FromLocation!.LocationName,
                    To = transfer.ToLocation!.LocationName,
                    transfer.TransferredAt,
                    Reason = transfer.TransferReason
                })
                .OrderBy(transfer => transfer.Docket)
                .ThenBy(transfer => transfer.Evidence)
                .ThenBy(transfer => transfer.TransferredAt)
                .ToListAsync(cancellationToken);

            var docketTotals = transfers.GroupBy(transfer => transfer.Docket).ToDictionary(group => group.Key, group => group.Count());
            var rows = transfers
                .Select(transfer => Row(
                    ("Docket", transfer.Docket),
                    ("Evidence", transfer.Evidence),
                    ("From", transfer.From),
                    ("To", transfer.To),
                    ("TransferredAt", transfer.TransferredAt),
                    ("Reason", transfer.Reason),
                    ("DocketTransferTotal", docketTotals[transfer.Docket])))
                .ToList();

            return new ReportResult(
                "custody-summary",
                "Chain-of-Custody Summary Report",
                "Operational Reporting",
                generatedBy,
                generatedWhere,
                CreateCriteria(("Station Scope", generatedWhere)),
                new[] { "Docket", "Evidence", "From", "To", "TransferredAt", "Reason", "DocketTransferTotal" },
                rows,
                Row(("Total Transfers", transfers.Count), ("Dockets With Transfers", docketTotals.Count)));
        }

        private async Task<ReportResult> BuildDocketCriteria(string generatedBy, string generatedWhere, int? stationScope, DateTime? from, DateTime? to, string? status, CancellationToken cancellationToken)
        {
            var query = _db.Dockets
                .AsNoTracking()
                .Include(docket => docket.DocketStatus)
                .Include(docket => docket.Station)
                .AsQueryable();

            if (stationScope.HasValue)
                query = query.Where(docket => docket.FK_StationID == stationScope.Value);

            if (from.HasValue)
                query = query.Where(docket => docket.DateCreated >= from.Value.Date);

            if (to.HasValue)
            {
                var end = to.Value.Date.AddDays(1);
                query = query.Where(docket => docket.DateCreated < end);
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(docket => docket.DocketStatus.StatusName == status);

            var dockets = await query
                .OrderByDescending(docket => docket.DateCreated)
                .Select(docket => new
                {
                    docket.DocketNumber,
                    docket.OBReferenceNumber,
                    Status = docket.DocketStatus.StatusName,
                    Station = docket.Station.StationName,
                    docket.PriorityLevel,
                    docket.DateCreated
                })
                .ToListAsync(cancellationToken);

            return new ReportResult(
                "docket-criteria",
                "Adjustable Docket Criteria Report",
                "Criteria-Based Reporting",
                generatedBy,
                generatedWhere,
                CreateCriteria(
                    ("Station Scope", generatedWhere),
                    ("From", from?.ToString("yyyy-MM-dd") ?? "Any"),
                    ("To", to?.ToString("yyyy-MM-dd") ?? "Any"),
                    ("Status", string.IsNullOrWhiteSpace(status) ? "Any" : status)),
                new[] { "DocketNumber", "OBReferenceNumber", "Status", "Station", "Priority", "DateCreated" },
                dockets.Select(docket => Row(
                    ("DocketNumber", docket.DocketNumber),
                    ("OBReferenceNumber", docket.OBReferenceNumber),
                    ("Status", docket.Status),
                    ("Station", docket.Station),
                    ("Priority", docket.PriorityLevel),
                    ("DateCreated", docket.DateCreated))).ToList(),
                Row(("Matched Dockets", dockets.Count), ("From", from?.ToString("yyyy-MM-dd") ?? "Any"), ("To", to?.ToString("yyyy-MM-dd") ?? "Any"), ("Status", status ?? "Any")));
        }

        private async Task<ReportResult> BuildStatusManagement(string generatedBy, string generatedWhere, int? stationScope, CancellationToken cancellationToken)
        {
            var query = _db.Dockets
                .AsNoTracking()
                .AsQueryable();

            if (stationScope.HasValue)
                query = query.Where(docket => docket.FK_StationID == stationScope.Value);

            var grouped = await query
                .Select(docket => new { Status = docket.DocketStatus.StatusName })
                .ToListAsync(cancellationToken);

            var total = grouped.Count;
            var rows = grouped
                .GroupBy(item => item.Status)
                .OrderByDescending(group => group.Count())
                .Select(group =>
                {
                    var count = group.Count();
                    var percent = total == 0 ? 0 : Math.Round(count * 100m / total, 1);
                    return Row(("Status", group.Key), ("Count", count), ("Percent", percent));
                })
                .ToList();

            return new ReportResult(
                "status-management",
                "Docket Status Distribution Report",
                "Operational Reporting",
                generatedBy,
                generatedWhere,
                CreateCriteria(("Station Scope", generatedWhere)),
                new[] { "Status", "Count", "Percent" },
                rows,
                Row(("Total Dockets", total)),
                rows,
                chartTitle: "Docket count by current status");
        }

        private async Task<ReportResult> BuildHotspotAnalysis(string generatedBy, string generatedWhere, int? stationScope, DateTime? from, DateTime? to, string? crimeCategory, CancellationToken cancellationToken)
        {
            var selectedCrimeCategory = crimeCategory?.Trim();
            var query = _db.Dockets
                .AsNoTracking()
                .AsQueryable();

            if (stationScope.HasValue)
                query = query.Where(docket => docket.FK_StationID == stationScope.Value);

            if (from.HasValue)
                query = query.Where(docket => docket.DateCreated >= from.Value.Date);

            if (to.HasValue)
            {
                var end = to.Value.Date.AddDays(1);
                query = query.Where(docket => docket.DateCreated < end);
            }

            if (!string.IsNullOrWhiteSpace(selectedCrimeCategory))
                query = query.Where(docket => docket.Incident != null &&
                    docket.Incident.IncidentType != null &&
                    docket.Incident.IncidentType.TypeName == selectedCrimeCategory);

            var data = await query
                .Select(docket => new
                {
                    Area = docket.Incident != null && docket.Incident.GeoLocation != null
                        ? docket.Incident.GeoLocation.AreaName ?? "Unspecified"
                        : "Unspecified",
                    CrimeCategory = docket.Incident != null && docket.Incident.IncidentType != null
                        ? docket.Incident.IncidentType.TypeName ?? "Unspecified"
                        : "Unspecified",
                    Station = docket.Station.StationName
                })
                .ToListAsync(cancellationToken);

            var rows = data
                .GroupBy(item => new { item.Area, item.CrimeCategory, item.Station })
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key.Area)
                .Select(group => Row(
                    ("Area", group.Key.Area),
                    ("CrimeCategory", group.Key.CrimeCategory),
                    ("Station", group.Key.Station),
                    ("Count", group.Count())))
                .ToList();

            return new ReportResult(
                "hotspot-analysis",
                "Hotspot Analysis Report",
                "Operational Intelligence",
                generatedBy,
                generatedWhere,
                CreateCriteria(
                    ("Station Scope", generatedWhere),
                    ("From", from?.ToString("yyyy-MM-dd") ?? "Any"),
                    ("To", to?.ToString("yyyy-MM-dd") ?? "Any"),
                    ("Crime Category", string.IsNullOrWhiteSpace(selectedCrimeCategory) ? "All" : selectedCrimeCategory)),
                new[] { "Area", "CrimeCategory", "Station", "Count" },
                rows,
                Row(
                    ("Total Incidents", data.Count),
                    ("Number of Hotspot Areas", data.Select(item => item.Area).Distinct().Count()),
                    ("Date Range", $"{(from?.ToString("yyyy-MM-dd") ?? "Any")} to {(to?.ToString("yyyy-MM-dd") ?? "Any")}")),
                rows.Take(15).ToList(),
                chartTitle: "Top hotspot combinations");
        }

        private async Task<ReportResult> BuildClearanceRate(string generatedBy, string generatedWhere, int? stationScope, DateTime? from, DateTime? to, CancellationToken cancellationToken)
        {
            var query = _db.Dockets
                .AsNoTracking()
                .AsQueryable();

            if (stationScope.HasValue)
                query = query.Where(docket => docket.FK_StationID == stationScope.Value);

            if (from.HasValue)
                query = query.Where(docket => docket.DateCreated >= from.Value.Date);

            if (to.HasValue)
            {
                var end = to.Value.Date.AddDays(1);
                query = query.Where(docket => docket.DateCreated < end);
            }

            var data = await query
                .Select(docket => new
                {
                    Station = docket.Station.StationName,
                    Status = docket.DocketStatus.StatusName
                })
                .ToListAsync(cancellationToken);

            var rows = data
                .GroupBy(item => item.Station)
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    var total = group.Count();
                    var cleared = group.Count(item => item.Status == "CLOSED" || item.Status == "ARCHIVED");
                    var rate = total == 0 ? 0 : Math.Round(cleared * 100m / total, 1);
                    return Row(("Station", group.Key), ("TotalCases", total), ("ClearedCases", cleared), ("ClearanceRate", rate));
                })
                .ToList();

            var totalCases = data.Count;
            var totalCleared = data.Count(item => item.Status == "CLOSED" || item.Status == "ARCHIVED");
            var overallRate = totalCases == 0 ? 0 : Math.Round(totalCleared * 100m / totalCases, 1);

            return new ReportResult(
                "clearance-rate",
                "Case Clearance Rate Report",
                "Performance Reporting",
                generatedBy,
                generatedWhere,
                CreateCriteria(
                    ("Station Scope", generatedWhere),
                    ("From", from?.ToString("yyyy-MM-dd") ?? "Any"),
                    ("To", to?.ToString("yyyy-MM-dd") ?? "Any")),
                new[] { "Station", "TotalCases", "ClearedCases", "ClearanceRate" },
                rows,
                Row(("Total Cases", totalCases), ("Cleared Cases", totalCleared), ("Overall Clearance Rate", overallRate)),
                rows,
                chartTitle: "Clearance rate by station");
        }

        private async Task<ReportResult> BuildCrimeTrend(string generatedBy, string generatedWhere, int? stationScope, DateTime? from, DateTime? to, CancellationToken cancellationToken)
        {
            var utcToday = DateTime.UtcNow.Date;
            var start = from?.Date ?? utcToday.AddMonths(-11).AddDays(1 - utcToday.Day);
            var end = (to?.Date ?? utcToday).AddDays(1);

            var query = _db.Dockets
                .AsNoTracking()
                .Where(docket => docket.DateCreated >= start && docket.DateCreated < end);

            if (stationScope.HasValue)
                query = query.Where(docket => docket.FK_StationID == stationScope.Value);

            var data = await query
                .Select(docket => new
                {
                    docket.DateCreated.Year,
                    docket.DateCreated.Month,
                    CrimeCategory = docket.Incident != null && docket.Incident.IncidentType != null
                        ? docket.Incident.IncidentType.TypeName ?? "Unspecified"
                        : "Unspecified"
                })
                .ToListAsync(cancellationToken);

            var rows = data
                .GroupBy(item => new { item.Year, item.Month, item.CrimeCategory })
                .OrderBy(group => group.Key.Year)
                .ThenBy(group => group.Key.Month)
                .ThenBy(group => group.Key.CrimeCategory)
                .Select(group => Row(
                    ("Month", $"{group.Key.Year:D4}-{group.Key.Month:D2}"),
                    ("CrimeCategory", group.Key.CrimeCategory),
                    ("Count", group.Count())))
                .ToList();

            return new ReportResult(
                "crime-trend",
                "Crime Trend Report",
                "Trend Analysis",
                generatedBy,
                generatedWhere,
                CreateCriteria(
                    ("Station Scope", generatedWhere),
                    ("From", start.ToString("yyyy-MM-dd")),
                    ("To", end.AddDays(-1).ToString("yyyy-MM-dd"))),
                new[] { "Month", "CrimeCategory", "Count" },
                rows,
                Row(
                    ("Cases in Period", data.Count),
                    ("From Date", start.ToString("yyyy-MM-dd")),
                    ("To Date", end.AddDays(-1).ToString("yyyy-MM-dd")),
                    ("Number of Crime Categories", data.Select(item => item.CrimeCategory).Distinct().Count())),
                rows,
                chartTitle: "Monthly crime trends");
        }

        private async Task<ReportResult> BuildResponseTime(string generatedBy, string generatedWhere, int? stationScope, DateTime? from, DateTime? to, CancellationToken cancellationToken)
        {
            var query = _db.Dockets
                .AsNoTracking()
                .Where(docket => docket.DateAllocated.HasValue);

            if (stationScope.HasValue)
                query = query.Where(docket => docket.FK_StationID == stationScope.Value);

            if (from.HasValue)
                query = query.Where(docket => docket.DateCreated >= from.Value.Date);

            if (to.HasValue)
            {
                var end = to.Value.Date.AddDays(1);
                query = query.Where(docket => docket.DateCreated < end);
            }

            var data = await query
                .Select(docket => new
                {
                    docket.DocketNumber,
                    Station = docket.Station.StationName,
                    docket.DateCreated,
                    docket.DateAllocated,
                    AcceptedAt = docket.Investigation == null
                        ? null
                        : docket.Investigation.InvestigationAssignments
                            .OrderByDescending(assignment => assignment.DateAssigned)
                            .Select(assignment => assignment.DateAccepted)
                            .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            var measured = data.Select(item => new
            {
                item.DocketNumber,
                item.Station,
                AllocationHours = Math.Round((item.DateAllocated!.Value - item.DateCreated).TotalHours, 1),
                AcceptanceHours = item.AcceptedAt.HasValue
                    ? Math.Round((item.AcceptedAt.Value - item.DateAllocated.Value).TotalHours, 1)
                    : (double?)null
            }).ToList();

            var rows = measured
                .OrderByDescending(item => item.AllocationHours)
                .Select(item => Row(
                    ("DocketNumber", item.DocketNumber),
                    ("Station", item.Station),
                    ("AllocationHours", item.AllocationHours),
                    ("AcceptanceHours", item.AcceptanceHours)))
                .ToList();

            return new ReportResult(
                "response-time",
                "Docket Response Time Report",
                "Performance Reporting",
                generatedBy,
                generatedWhere,
                CreateCriteria(
                    ("Station Scope", generatedWhere),
                    ("From", from?.ToString("yyyy-MM-dd") ?? "Any"),
                    ("To", to?.ToString("yyyy-MM-dd") ?? "Any")),
                new[] { "DocketNumber", "Station", "AllocationHours", "AcceptanceHours" },
                rows,
                Row(
                    ("Allocated Dockets", measured.Count),
                    ("Average Allocation Hours", measured.Count == 0 ? 0 : Math.Round(measured.Average(item => item.AllocationHours), 1)),
                    ("Average Acceptance Hours", measured.Any(item => item.AcceptanceHours.HasValue)
                        ? Math.Round(measured.Where(item => item.AcceptanceHours.HasValue).Average(item => item.AcceptanceHours!.Value), 1)
                        : 0)));
        }

        private async Task<ReportResult> BuildCaseAging(string generatedBy, string generatedWhere, int? stationScope, DateTime? from, DateTime? to, CancellationToken cancellationToken)
        {
            var query = _db.Dockets
                .AsNoTracking()
                .Where(docket => docket.DocketStatus.StatusName != "CLOSED" && docket.DocketStatus.StatusName != "ARCHIVED");

            if (stationScope.HasValue)
                query = query.Where(docket => docket.FK_StationID == stationScope.Value);

            if (from.HasValue)
                query = query.Where(docket => docket.DateCreated >= from.Value.Date);

            if (to.HasValue)
            {
                var end = to.Value.Date.AddDays(1);
                query = query.Where(docket => docket.DateCreated < end);
            }

            var data = await query
                .Select(docket => new
                {
                    docket.DocketNumber,
                    Station = docket.Station.StationName,
                    Status = docket.DocketStatus.StatusName,
                    docket.DateCreated,
                    Officer = docket.Investigation == null
                        ? "Unallocated"
                        : docket.Investigation.InvestigationAssignments
                            .OrderByDescending(assignment => assignment.DateAssigned)
                            .Select(assignment => assignment.User == null
                                ? "Unallocated"
                                : (assignment.User.FirstName + " " + assignment.User.LastName).Trim())
                            .FirstOrDefault() ?? "Unallocated"
                })
                .ToListAsync(cancellationToken);

            var today = DateTime.UtcNow;
            var aged = data.Select(item => new
            {
                item.DocketNumber,
                item.Station,
                item.Status,
                item.Officer,
                AgeDays = Math.Max(0, (int)(today - item.DateCreated).TotalDays)
            }).ToList();

            var rows = aged
                .OrderByDescending(item => item.AgeDays)
                .Select(item => Row(
                    ("DocketNumber", item.DocketNumber),
                    ("Station", item.Station),
                    ("Officer", item.Officer),
                    ("Status", item.Status),
                    ("AgeDays", item.AgeDays),
                    ("AgeBand", item.AgeDays <= 7 ? "0-7" : item.AgeDays <= 30 ? "8-30" : item.AgeDays <= 90 ? "31-90" : "90+")))
                .ToList();

            var chart = aged
                .GroupBy(item => item.Officer)
                .OrderByDescending(group => group.Count())
                .Select(group => Row(("Officer", group.Key), ("Count", group.Count()), ("OldestDays", group.Max(item => item.AgeDays))))
                .ToList();

            return new ReportResult(
                "case-aging",
                "Workload and Case Aging Report",
                "Operational Workload",
                generatedBy,
                generatedWhere,
                CreateCriteria(
                    ("Station Scope", generatedWhere),
                    ("From", from?.ToString("yyyy-MM-dd") ?? "Any"),
                    ("To", to?.ToString("yyyy-MM-dd") ?? "Any"),
                    ("Status Exclusion", "CLOSED, ARCHIVED")),
                new[] { "DocketNumber", "Station", "Officer", "Status", "AgeDays", "AgeBand" },
                rows,
                Row(
                    ("Active Cases", aged.Count),
                    ("Cases Over 30 Days", aged.Count(item => item.AgeDays > 30)),
                    ("Cases Over 90 Days", aged.Count(item => item.AgeDays > 90))),
                chart,
                chartTitle: "Active workload by officer");
        }

        private ReportPdfRequest CreatePdfRequest(ReportResult report, byte[] chartImage)
        {
            return new ReportPdfRequest
            {
                Key = report.Key,
                Title = report.Title,
                Category = report.Category,
                GeneratedBy = report.GeneratedBy,
                GeneratedWhere = report.GeneratedWhere,
                GeneratedAt = report.GeneratedAt,
                Criteria = report.Criteria,
                Columns = report.Columns,
                Rows = report.Rows,
                Totals = report.Totals,
                ChartImage = chartImage,
                ChartTitle = report.ChartTitle,
                Landscape = report.Landscape
            };
        }

        private byte[] BuildChartImage(ReportResult report)
        {
            return report.Key.ToLowerInvariant() switch
            {
                "dockets-list" => _chartService.CreateStatusBarChart(
                    report.Rows
                        .GroupBy(row => Format(GetValue(row, "Status")))
                        .Select(group => new ReportPdfChartPoint(group.Key, group.Count()))
                        .OrderByDescending(point => point.Value)
                        .ToList(),
                    report.ChartTitle ?? "Docket count by current status"),
                "status-management" => _chartService.CreateStatusBarChart(
                    report.Rows.Select(row => new ReportPdfChartPoint(Format(GetValue(row, "Status")), AsNumber(GetValue(row, "Count")))).ToList(),
                    report.ChartTitle ?? "Docket count by current status"),
                "hotspot-analysis" => _chartService.CreateHotspotBarChart(
                    (report.Chart ?? Array.Empty<Dictionary<string, object?>>())
                        .Select(row => new ReportPdfChartPoint(
                            $"{Format(GetValue(row, "Area"))} - {Format(GetValue(row, "CrimeCategory"))}",
                            AsNumber(GetValue(row, "Count"))))
                        .ToList(),
                    report.ChartTitle ?? "Top hotspot combinations"),
                "clearance-rate" => _chartService.CreateClearanceRateChart(report.Rows, report.ChartTitle ?? "Clearance rate by station"),
                "crime-trend" => _chartService.CreateCrimeTrendLineChart(report.Rows, report.ChartTitle ?? "Monthly crime trends"),
                "case-aging" => _chartService.CreateOfficerWorkloadChart(
                    (report.Chart ?? Array.Empty<Dictionary<string, object?>>())
                        .Select(row => new ReportPdfChartPoint(Format(GetValue(row, "Officer")), AsNumber(GetValue(row, "Count"))))
                        .ToList(),
                    report.ChartTitle ?? "Active workload by officer"),
                _ => Array.Empty<byte>()
            };
        }

        private static bool HasInvalidDateRange(DateTime? from, DateTime? to)
        {
            return from.HasValue && to.HasValue && from.Value.Date > to.Value.Date;
        }

        private static Dictionary<string, string> CreateCriteria(params (string Key, string Value)[] items)
        {
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .ToDictionary(item => item.Key, item => item.Value);
        }

        private static Dictionary<string, object?> Row(params (string Key, object? Value)[] values)
        {
            return values.ToDictionary(item => item.Key, item => item.Value);
        }

        private static object? GetValue(IReadOnlyDictionary<string, object?> row, string key)
        {
            return row.TryGetValue(key, out var value) ? value : null;
        }

        private static double AsNumber(object? value)
        {
            return value switch
            {
                byte number => number,
                short number => number,
                int number => number,
                long number => number,
                float number => number,
                double number => number,
                decimal number => (double)number,
                _ when double.TryParse(value?.ToString(), out var parsed) => parsed,
                _ => 0d
            };
        }

        private static string Format(object? value)
        {
            return value switch
            {
                null => "—",
                DateTime date => date.ToString("yyyy-MM-dd HH:mm"),
                decimal number => number.ToString("0.0"),
                _ => string.IsNullOrWhiteSpace(value.ToString()) ? "—" : value.ToString()!
            };
        }

        private static string BuildFileName(ReportResult report)
        {
            var baseName = report.Key.ToLowerInvariant() switch
            {
                "dockets-list" => "Dockets-List",
                "station-workload" => "Station-Workload",
                "hotspot-analysis" => "Hotspot-Analysis",
                "clearance-rate" => "Clearance-Rate",
                "crime-trend" => "Crime-Trend",
                "case-aging" => "Workload-Case-Aging",
                _ => string.Join("-", report.Title.Replace("Report", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            };

            return $"{baseName}-{report.GeneratedAt:yyyyMMdd-HHmm}.pdf";
        }

        private string? GetClientIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            return userIdClaim != null && int.TryParse(userIdClaim, out var userId)
                ? userId
                : 0;
        }

        public class ReportResult
        {
            public ReportResult(
                string key,
                string title,
                string category,
                string generatedBy,
                string generatedWhere,
                Dictionary<string, string> criteria,
                IReadOnlyList<string> columns,
                IReadOnlyList<Dictionary<string, object?>> rows,
                Dictionary<string, object?> totals,
                IReadOnlyList<Dictionary<string, object?>>? chart = null,
                string? chartTitle = null,
                bool landscape = true)
            {
                Key = key;
                Title = title;
                Category = category;
                GeneratedBy = generatedBy;
                GeneratedWhere = generatedWhere;
                Criteria = criteria;
                Columns = columns;
                Rows = rows;
                Totals = totals;
                Chart = chart;
                ChartTitle = chartTitle;
                Landscape = landscape;
            }

            public string Key { get; }
            public string Title { get; }
            public string Category { get; }
            public DateTime GeneratedAt { get; } = DateTime.UtcNow;
            public string GeneratedBy { get; }
            public string GeneratedWhere { get; }
            public Dictionary<string, string> Criteria { get; }
            public IReadOnlyList<string> Columns { get; }
            public IReadOnlyList<Dictionary<string, object?>> Rows { get; }
            public Dictionary<string, object?> Totals { get; }
            public IReadOnlyList<Dictionary<string, object?>>? Chart { get; }
            public string? ChartTitle { get; }
            public bool Landscape { get; }
        }
    }
}
