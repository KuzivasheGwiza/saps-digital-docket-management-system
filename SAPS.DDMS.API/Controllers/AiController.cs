using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;
using System.Security.Claims;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAiService _aiService;
        private readonly IAuditService _auditService;

        public AiController(ApplicationDbContext db, IAiService aiService, IAuditService auditService)
        {
            _db = db;
            _aiService = aiService;
            _auditService = auditService;
        }

        [HttpPost("docket/{docketId}/summary")]
        [Authorize(Roles = "Investigating Officer,DSC Supervisor,Docket Administrator")]
        public async Task<IActionResult> GenerateSummary(int docketId)
        {
            var userId = GetCurrentUserId();
            if (!await _db.Dockets.AnyAsync(d => d.DocketID == docketId))
                return NotFound(new { success = false, message = "Docket not found." });
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            var summary = await _aiService.SummarizeDocketAsync(docketId, userId);
            var log = new AIInteractionLog
            {
                FK_DocketID = docketId,
                ActionType = "GENERATE_SUMMARY",
                Payload = summary,
                Timestamp = DateTime.UtcNow,
                PerformedByUserID = userId
            };
            await _db.AIInteractionLogs.AddAsync(log);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("GENERATE_AI_SUMMARY", "Docket", docketId, null, new { summary }, userId);

            return Ok(new { summary, logId = log.AIInteractionLogID });
        }

        public class ConfirmSummaryDto
        {
            [Required, MinLength(10), MaxLength(10000)]
            public string Summary { get; set; } = string.Empty;
        }

        [HttpPost("docket/{docketId}/summary/confirm")]
        [Authorize(Roles = "Investigating Officer,DSC Supervisor,Docket Administrator")]
        public async Task<IActionResult> ConfirmSummary(int docketId, [FromBody] ConfirmSummaryDto dto)
        {
            var userId = GetCurrentUserId();
            if (!await _db.Dockets.AnyAsync(d => d.DocketID == docketId))
                return NotFound(new { success = false, message = "Docket not found." });
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            var summary = dto?.Summary?.Trim() ?? string.Empty;
            if (summary.Length < 10)
                return BadRequest(new { success = false, message = "Review and provide a summary of at least 10 characters." });

            var generated = await _db.AIInteractionLogs.AnyAsync(log =>
                log.FK_DocketID == docketId &&
                log.PerformedByUserID == userId &&
                log.ActionType == "GENERATE_SUMMARY");
            if (!generated)
                return BadRequest(new { success = false, message = "Generate a summary before confirming it." });

            await _db.AIInteractionLogs.AddAsync(new AIInteractionLog
            {
                FK_DocketID = docketId,
                ActionType = "CONFIRM_SUMMARY",
                Payload = summary,
                Timestamp = DateTime.UtcNow,
                PerformedByUserID = userId
            });
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("CONFIRM_AI_SUMMARY", "Docket", docketId, null,
                new { Summary = summary, HumanConfirmed = true }, userId,
                "AI summary reviewed and confirmed by an authorised user");

            return Ok(new { success = true, message = "Reviewed AI summary confirmed." });
        }

        [HttpPost("docket/{docketId}/extract")]
        [Authorize(Roles = "Investigating Officer,Docket Administrator")]
        public async Task<IActionResult> ExtractDocketData(int docketId)
        {
            var userId = GetCurrentUserId();
            if (!await _db.Dockets.AnyAsync(d => d.DocketID == docketId))
                return NotFound(new { success = false, message = "Docket not found." });
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            var extraction = await _aiService.ExtractDocketDataAsync(docketId, userId);
            if (extraction.Count == 0)
                return NotFound(new { success = false, message = "Docket data could not be extracted." });

            await _db.AIInteractionLogs.AddAsync(new AIInteractionLog
            {
                FK_DocketID = docketId,
                ActionType = "EXTRACT_DOCKET_DATA",
                Payload = JsonSerializer.Serialize(extraction),
                Timestamp = DateTime.UtcNow,
                PerformedByUserID = userId
            });
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("GENERATE_AI_EXTRACTION", "Docket", docketId, null,
                new { Fields = extraction.Keys }, userId);

            return Ok(new { extraction, requiresHumanVerification = true });
        }

        [HttpPost("docket/{docketId}/verify-extractions")]
        [Authorize(Roles = "Investigating Officer,Docket Administrator")]
        public async Task<IActionResult> VerifyExtractions(int docketId, [FromBody] JsonElement verifiedValues)
        {
            var userId = GetCurrentUserId();
            if (!await _db.Dockets.AnyAsync(d => d.DocketID == docketId))
                return NotFound(new { success = false, message = "Docket not found." });
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();
            if (verifiedValues.ValueKind != JsonValueKind.Object)
                return BadRequest(new { success = false, message = "Verified extraction data must be a JSON object." });

            var verified = await _aiService.VerifyExtractionsAsync(docketId, verifiedValues, userId);
            if (!verified)
                return BadRequest(new { success = false, message = "Generate an extraction before verifying it." });
            var payload = verifiedValues.GetRawText();
            await _db.AIInteractionLogs.AddAsync(new AIInteractionLog
            {
                FK_DocketID = docketId,
                ActionType = "VERIFY_EXTRACTION",
                Payload = payload,
                Timestamp = DateTime.UtcNow,
                PerformedByUserID = userId
            });
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("VERIFY_AI_EXTRACTED_DATA", "Docket", docketId, null, payload, userId);

            return Ok(new { success = true, message = "AI extracted data verified." });
        }

        [HttpPost("docket/{docketId}/related-cases")]
        [Authorize(Roles = "Investigating Officer,DSC Supervisor,Docket Administrator")]
        public async Task<IActionResult> SuggestRelatedCases(int docketId, [FromQuery] int maxResults = 5)
        {
            var userId = GetCurrentUserId();
            if (!await _db.Dockets.AnyAsync(d => d.DocketID == docketId))
                return NotFound(new { success = false, message = "Docket not found." });
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            var suggestions = (await _aiService.SuggestRelatedCasesAsync(docketId, maxResults)).ToList();
            var payload = suggestions.Select(s => new { s.relatedDocketId, s.score, s.reason }).ToList();
            await _db.AIInteractionLogs.AddAsync(new AIInteractionLog
            {
                FK_DocketID = docketId,
                ActionType = "SUGGEST_RELATED_CASES",
                Payload = JsonSerializer.Serialize(payload),
                Timestamp = DateTime.UtcNow,
                PerformedByUserID = userId
            });
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("LINK_RELATED_CASES_AI_SUGGESTIONS", "Docket", docketId, null, payload, userId);

            return Ok(payload);
        }

        public class ConfirmedRelatedCaseDto
        {
            public int RelatedDocketId { get; set; }
            [Range(0, 1)]
            public double Score { get; set; }
            [Required, MinLength(3), MaxLength(500)]
            public string Reason { get; set; } = string.Empty;
        }

        public class ConfirmRelatedCasesDto
        {
            [Required, MinLength(1)]
            public List<ConfirmedRelatedCaseDto> RelatedCases { get; set; } = new();
        }

        [HttpPost("docket/{docketId}/related-cases/confirm")]
        [Authorize(Roles = "Investigating Officer,DSC Supervisor,Docket Administrator")]
        public async Task<IActionResult> ConfirmRelatedCases(int docketId, [FromBody] ConfirmRelatedCasesDto dto)
        {
            var userId = GetCurrentUserId();
            if (!await _db.Dockets.AnyAsync(d => d.DocketID == docketId))
                return NotFound(new { success = false, message = "Docket not found." });
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            var generated = await _db.AIInteractionLogs.AnyAsync(log =>
                log.FK_DocketID == docketId &&
                log.PerformedByUserID == userId &&
                log.ActionType == "SUGGEST_RELATED_CASES");
            if (!generated)
                return BadRequest(new { success = false, message = "Generate related case suggestions before confirming them." });

            var reviewed = (dto?.RelatedCases ?? new List<ConfirmedRelatedCaseDto>())
                .Where(item => item.RelatedDocketId > 0 && item.RelatedDocketId != docketId)
                .GroupBy(item => item.RelatedDocketId)
                .Select(group => group.First())
                .ToList();
            if (reviewed.Count == 0 || reviewed.Any(item => string.IsNullOrWhiteSpace(item.Reason)))
                return BadRequest(new { success = false, message = "Confirm at least one reviewed related case with a reason." });

            var relatedIds = reviewed.Select(item => item.RelatedDocketId).ToList();
            var requester = await _db.Users.Include(user => user.Role).FirstAsync(user => user.UserID == userId);
            var allowedIdsQuery = _db.Dockets.Where(item => relatedIds.Contains(item.DocketID));
            if (!string.Equals(requester.Role?.Name, "System Administrator", StringComparison.OrdinalIgnoreCase))
                allowedIdsQuery = allowedIdsQuery.Where(item => item.FK_StationID == requester.FK_StationID);
            var allowedIds = (await allowedIdsQuery.Select(item => item.DocketID).ToListAsync()).ToHashSet();
            if (allowedIds.Count != relatedIds.Distinct().Count())
                return BadRequest(new { success = false, message = "One or more related dockets are unavailable to the current user." });

            foreach (var item in reviewed)
            {
                await _db.AISuggestions.AddAsync(new AISuggestion
                {
                    FK_DocketID = docketId,
                    SuggestionType = "RELATED_CASE_CONFIRMED",
                    Content = $"Docket {item.RelatedDocketId}: {item.Reason.Trim()}",
                    Score = item.Score,
                    Timestamp = DateTime.UtcNow
                });
            }

            var payload = JsonSerializer.Serialize(reviewed);
            await _db.AIInteractionLogs.AddAsync(new AIInteractionLog
            {
                FK_DocketID = docketId,
                ActionType = "CONFIRM_RELATED_CASES",
                Payload = payload,
                Timestamp = DateTime.UtcNow,
                PerformedByUserID = userId
            });
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("CONFIRM_AI_RELATED_CASES", "Docket", docketId, null,
                new { RelatedCases = reviewed, HumanConfirmed = true }, userId,
                "AI related-case suggestions reviewed and confirmed by an authorised user");

            return Ok(new { success = true, count = reviewed.Count, message = "Reviewed related cases confirmed." });
        }

        [HttpPost("docket/{docketId}/readiness-intelligence")]
        [Authorize(Roles = "Investigating Officer,DSC Supervisor,Docket Administrator,Prosecutor")]
        public async Task<IActionResult> GenerateReadinessIntelligence(int docketId)
        {
            var userId = GetCurrentUserId();
            var docket = await _db.Dockets.AsNoTracking()
                .Include(item => item.DocketStatus)
                .FirstOrDefaultAsync(item => item.DocketID == docketId);
            if (docket == null)
                return NotFound(new { success = false, message = "Docket not found." });
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            var evidence = await _db.Evidences.AsNoTracking()
                .Where(item => item.FK_DocketID == docketId)
                .Select(item => new
                {
                    item.EvidenceID,
                    HasIntegrityHash = !string.IsNullOrWhiteSpace(item.PayloadHash) || !string.IsNullOrWhiteSpace(item.FileHash),
                    HasCollectionControls = !string.IsNullOrWhiteSpace(item.SignatureMethod) &&
                        !string.IsNullOrWhiteSpace(item.CollectionMethod) && item.FK_EvidenceLocationID > 0
                })
                .ToListAsync();
            var evidenceIds = evidence.Select(item => item.EvidenceID).ToList();
            var transferCount = evidenceIds.Count == 0 ? 0 : await _db.EvidenceTransfers.AsNoTracking()
                .CountAsync(item => evidenceIds.Contains(item.FK_EvidenceID));
            var noteCount = docket.FK_InvestigationID.HasValue
                ? await _db.InvestigationNotes.AsNoTracking().CountAsync(item =>
                    item.FK_InvestigationID == docket.FK_InvestigationID.Value && item.Status != "Archived")
                : 0;
            var assignment = docket.FK_InvestigationID.HasValue
                ? await _db.InvestigationAssignments.AsNoTracking()
                    .Where(item => item.FK_InvestigationID == docket.FK_InvestigationID.Value)
                    .OrderByDescending(item => item.DateAssigned)
                    .Select(item => new { item.DateAccepted, item.InspectionDueAt, item.InspectionCompletedAt, item.AllocationReviewStatus })
                    .FirstOrDefaultAsync()
                : null;
            var bundleStatus = await _db.ProsecutorBundles.AsNoTracking()
                .Where(item => item.FK_DocketID == docketId)
                .OrderByDescending(item => item.Version)
                .Select(item => item.Status)
                .FirstOrDefaultAsync();
            var timerDueAt = await _db.DocketTimers.AsNoTracking()
                .Where(item => item.FK_DocketID == docketId && !item.IsCompleted)
                .OrderByDescending(item => item.StartTime)
                .Select(item => item.DurationLimit)
                .FirstOrDefaultAsync();

            var evidenceCount = evidence.Count;
            var hashedCount = evidence.Count(item => item.HasIntegrityHash);
            var controlledCount = evidence.Count(item => item.HasCollectionControls);
            var integrityCoverage = evidenceCount == 0 ? 0 : (int)Math.Round(hashedCount * 100d / evidenceCount);
            var custodyCoverage = evidenceCount == 0 ? 0 : (int)Math.Round(controlledCount * 100d / evidenceCount);
            var score = 0;
            if (evidenceCount > 0) score += 15;
            score += (int)Math.Round(integrityCoverage * 0.20);
            score += (int)Math.Round(custodyCoverage * 0.15);
            if (noteCount > 0) score += 15;
            if (assignment?.DateAccepted != null) score += 15;
            if (assignment?.InspectionCompletedAt != null) score += 5;
            if (string.Equals(bundleStatus, "Exported", StringComparison.OrdinalIgnoreCase)) score += 15;
            else if (!string.IsNullOrWhiteSpace(bundleStatus)) score += 5;
            score = Math.Clamp(score, 0, 100);

            var blockers = new List<string>();
            var recommendations = new List<string>();
            if (evidenceCount == 0) blockers.Add("No evidence has been recorded for this docket.");
            if (evidenceCount > 0 && hashedCount < evidenceCount)
                blockers.Add($"{evidenceCount - hashedCount} evidence item(s) do not have a recorded integrity hash.");
            if (evidenceCount > 0 && controlledCount < evidenceCount)
                blockers.Add($"{evidenceCount - controlledCount} evidence item(s) have incomplete collection/signature/location controls.");
            if (noteCount == 0) blockers.Add("No active investigation note records progress or findings.");
            if (assignment == null) blockers.Add("No investigating-officer assignment exists.");
            else if (assignment.DateAccepted == null) blockers.Add("The latest officer allocation has not been accepted by the assigned officer.");
            if (assignment?.InspectionCompletedAt == null) recommendations.Add("Complete and record the supervisory docket inspection.");
            if (string.IsNullOrWhiteSpace(bundleStatus)) recommendations.Add("Create a prosecutor bundle after the investigation record is complete.");
            else if (!string.Equals(bundleStatus, "Exported", StringComparison.OrdinalIgnoreCase))
                recommendations.Add("Review and export the current prosecutor bundle.");
            if (transferCount == 0 && evidenceCount > 0)
                recommendations.Add("Record every future evidence movement in the chain-of-custody register.");
            if (blockers.Count == 0) recommendations.Add("No automated readiness blocker was detected; complete human prosecutorial review.");

            var now = DateTime.UtcNow;
            var slaState = !timerDueAt.HasValue ? "Not configured"
                : timerDueAt.Value < now ? "Overdue"
                : timerDueAt.Value <= now.AddHours(24) ? "Due within 24 hours"
                : "On track";
            var readinessBand = score >= 80 ? "High" : score >= 55 ? "Moderate" : "Low";
            var result = new
            {
                docketId,
                docketNumber = docket.DocketNumber,
                generatedAtUtc = now,
                score,
                readinessBand,
                slaState,
                slaDueAt = timerDueAt,
                metrics = new
                {
                    evidenceCount,
                    hashedEvidenceCount = hashedCount,
                    integrityCoverage,
                    custodyControlCoverage = custodyCoverage,
                    evidenceTransferCount = transferCount,
                    activeInvestigationNotes = noteCount,
                    assignmentAccepted = assignment?.DateAccepted != null,
                    inspectionCompleted = assignment?.InspectionCompletedAt != null,
                    prosecutorBundleStatus = bundleStatus ?? "Not created"
                },
                blockers,
                recommendations,
                limitations = "Decision support only. The score is explainable and deterministic; an authorised person must verify the source records."
            };

            await _db.AIInteractionLogs.AddAsync(new AIInteractionLog
            {
                FK_DocketID = docketId,
                ActionType = "READINESS_INTELLIGENCE",
                Payload = JsonSerializer.Serialize(result),
                Timestamp = now,
                PerformedByUserID = userId
            });
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("GENERATE_CASE_READINESS_INTELLIGENCE", "Docket", docketId,
                null, new { score, readinessBand, integrityCoverage, custodyCoverage, BlockerCount = blockers.Count }, userId,
                "Explainable case-readiness decision support generated; human review remains required.");

            return Ok(result);
        }

        [HttpGet("logs")]
        [Authorize(Policy = PermissionCatalog.AiLogView)]
        public async Task<IActionResult> GetAiLogs([FromQuery] int? docketId)
        {
            var query = _db.AIInteractionLogs.Include(l => l.Docket).AsQueryable();
            var userId = GetCurrentUserId();
            var requester = await _db.Users.Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.UserID == userId);
            if (requester == null) return Forbid();
            if (!string.Equals(requester.Role?.Name, "System Administrator", StringComparison.OrdinalIgnoreCase))
                query = query.Where(log => log.Docket != null && log.Docket.FK_StationID == requester.FK_StationID);
            if (docketId.HasValue)
                query = query.Where(l => l.FK_DocketID == docketId.Value);

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Take(500)
                .Select(l => new
                {
                    l.AIInteractionLogID,
                    l.FK_DocketID,
                    DocketNumber = l.Docket!.DocketNumber,
                    l.ActionType,
                    l.Payload,
                    l.Timestamp,
                    l.PerformedByUserID
                })
                .ToListAsync();

            return Ok(logs);
        }

        private async Task<bool> CanAccessDocketAsync(int docketId, int userId)
        {
            var requester = await _db.Users.Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.UserID == userId);
            if (requester == null) return false;
            if (string.Equals(requester.Role?.Name, "System Administrator", StringComparison.OrdinalIgnoreCase))
                return true;

            var docket = await _db.Dockets
                .Include(item => item.Investigation)
                    .ThenInclude(investigation => investigation.InvestigationAssignments)
                .FirstOrDefaultAsync(item => item.DocketID == docketId);
            if (docket == null || docket.FK_StationID != requester.FK_StationID) return false;
            return !string.Equals(requester.Role?.Name, "Investigating Officer", StringComparison.OrdinalIgnoreCase) ||
                   docket.Investigation?.InvestigationAssignments?.Any(assignment => assignment.FK_UserID == userId) == true;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : 0;
        }
    }
}
