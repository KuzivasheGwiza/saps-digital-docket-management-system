using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Helpers;
using SAPS.DDMS.API.Security;
using SAPS.DDMS.API.Services;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/reference-data")]
    [Authorize(Policy = PermissionCatalog.DocketsView)]
    public class ReferenceDataController : ControllerBase
    {
        private const string DefaultCourtOutcomes =
            "Guilty|Not Guilty|Case Withdrawn|Case Struck Off";

        private readonly ApplicationDbContext _db;
        private readonly ISystemConfigService _configuration;

        public ReferenceDataController(
            ApplicationDbContext db,
            ISystemConfigService configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        [HttpGet("operational")]
        public async Task<IActionResult> GetOperationalLookups()
        {
            var statuses = await _db.DocketStatuses
                .AsNoTracking()
                .OrderBy(status => status.DocketStatusID)
                .Select(status => status.StatusName)
                .Where(status => status != null && status != string.Empty)
                .ToListAsync();
            var provinces = await _db.Stations
                .AsNoTracking()
                .Where(station => station.Province != null && station.Province != string.Empty)
                .Select(station => station.Province!)
                .Distinct()
                .OrderBy(province => province)
                .ToListAsync();
            var configuredOutcomes = await _configuration.GetStringAsync(
                "COURT_OUTCOME_OPTIONS",
                DefaultCourtOutcomes);
            var courtOutcomes = configuredOutcomes
                .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var runtimePolicies = new
            {
                docketPriorityHighDays = await _configuration.GetIntAsync("DOCKET_PRIORITY_HIGH_DAYS", 7),
                archiveEligibleDays = await _configuration.GetIntAsync("ARCHIVE_ELIGIBLE_DAYS", 90),
                evidenceRetentionYears = await _configuration.GetIntAsync("EVIDENCE_RETENTION_YEARS", 7),
                inspectionReminderHours = await _configuration.GetIntAsync("INSPECTION_REMINDER_HOURS", 4),
                inspectionSlaHours = await _configuration.GetIntAsync("INSPECTION_SLA_HOURS", 24),
                maxActiveDocketsPerOfficer = await _configuration.GetIntAsync("MAX_ACTIVE_DOCKETS_PER_OFFICER", 10)
            };

            return Ok(new
            {
                docketStatuses = statuses,
                provinces,
                courtOutcomes,
                incidentLocationTypes = OperationalLookupCatalog.IncidentLocationTypes,
                evidenceCollectionMethods = OperationalLookupCatalog.EvidenceCollectionMethods,
                evidenceItemStates = OperationalLookupCatalog.EvidenceItemStates,
                runtimePolicies
            });
        }
    }
}
