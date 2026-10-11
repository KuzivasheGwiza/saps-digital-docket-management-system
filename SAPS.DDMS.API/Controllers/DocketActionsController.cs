using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Services;
using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Authorize(Roles = "CSC Officer,DSC Supervisor,Investigating Officer,Docket Administrator,Records Clerk,Prosecutor")]
    public class DocketActionsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;
        private readonly IAiService _ai;
        private readonly ISystemConfigService _config;
        private readonly IReportPdfService _pdfService;
        private readonly IFileStorageService _fileStorage;

        public DocketActionsController(ApplicationDbContext db, IAuditService audit, IAiService ai, ISystemConfigService config, IReportPdfService pdfService, IFileStorageService fileStorage)
        {
            _db = db;
            _audit = audit;
            _ai = ai;
            _config = config;
            _pdfService = pdfService;
            _fileStorage = fileStorage;
        }

        private sealed record ManifestEntry(string Section, int Order, string Name, bool IsPrivate = false, string? PrivacyReason = null);

        private static string TrimManifestName(string value)
        {
            return value.Length <= 256 ? value : value[..253] + "...";
        }

        private static bool HasActiveDocketPrivacyFlag(Docket docket)
        {
            return docket.DocketPrivacyFlags?.Any(f => f.RemovedAt == null) == true;
        }

        private async Task<int> GetMaxActiveDocketsPerOfficerAsync()
        {
            var configuredValue = await _config.GetIntAsync("MAX_ACTIVE_DOCKETS_PER_OFFICER", 10);
            return configuredValue > 0 ? configuredValue : 10;
        }

        private async Task<int> GetOfficerActiveDocketCountAsync(int officerUserId)
        {
            if (officerUserId <= 0)
            {
                return 0;
            }

            var latestAssignments = (await _db.InvestigationAssignments
                    .AsNoTracking()
                    .Where(assignment => assignment.FK_UserID == officerUserId)
                    .Select(assignment => new
                    {
                        assignment.FK_InvestigationID,
                        assignment.DateAssigned,
                        assignment.AssignmentID
                    })
                    .ToListAsync())
                .GroupBy(assignment => assignment.FK_InvestigationID)
                .Select(group => group
                    .OrderByDescending(assignment => assignment.DateAssigned)
                    .ThenByDescending(assignment => assignment.AssignmentID)
                    .First())
                .ToList();

            if (!latestAssignments.Any())
            {
                return 0;
            }

            var investigationIds = latestAssignments
                .Select(assignment => assignment.FK_InvestigationID)
                .Distinct()
                .ToList();

            return await (
                from docket in _db.Dockets.AsNoTracking()
                join status in _db.DocketStatuses.AsNoTracking()
                    on docket.FK_DocketStatusID equals status.DocketStatusID
                where docket.FK_InvestigationID.HasValue &&
                      investigationIds.Contains(docket.FK_InvestigationID.Value) &&
                      status.StatusName != "OPEN" &&
                      status.StatusName != "CLOSED" &&
                      status.StatusName != "ARCHIVED"
                select docket.DocketID)
                .Distinct()
                .CountAsync();
        }

        private static List<ManifestEntry> BuildManifestEntries(Docket docket, bool includeEvidence = true, bool includeNotes = true)
        {
            var activePrivacyFlag = HasActiveDocketPrivacyFlag(docket);
            var entries = new List<ManifestEntry>
            {
                new("A-clip", 1, $"Docket cover: {docket.DocketNumber ?? docket.DocketID.ToString()}"),
                new("A-clip", 2, $"Registration references: OB {docket.OBReferenceNumber ?? "-"} / FIC {docket.FICReferenceNumber ?? "-"}"),
                new("A-clip", 3, TrimManifestName($"Docket summary: {docket.DocketSummary ?? "No summary captured"}"), activePrivacyFlag, activePrivacyFlag ? "Active docket privacy flag" : null)
            };

            if (includeEvidence)
            {
                var evidenceOrder = 1;
                foreach (var evidence in (docket.Evidences ?? Enumerable.Empty<Evidence>())
                    .OrderBy(e => e.EvidenceReferenceNumber)
                    .ThenBy(e => e.EvidenceID))
                {
                    var reference = evidence.EvidenceReferenceNumber ?? $"Evidence {evidence.EvidenceID}";
                    entries.Add(new ManifestEntry("B-clip", evidenceOrder++, TrimManifestName($"{reference}: {evidence.Description ?? "Evidence item"}")));
                }

                if (evidenceOrder == 1)
                    entries.Add(new("B-clip", 1, "Evidence index: no evidence items captured"));
            }

            if (includeNotes)
            {
                var noteOrder = 1;
                foreach (var note in (docket.Investigation?.InvestigationNotes ?? Enumerable.Empty<InvestigationNote>())
                    .Where(n => !string.Equals(n.Status, "Archived", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n.EntryDate)
                    .ThenBy(n => n.NoteID))
                {
                    entries.Add(new ManifestEntry("C-clip", noteOrder++,
                        TrimManifestName($"{note.EntryDate:yyyy-MM-dd}: {note.Description}")));
                }

                if (noteOrder == 1)
                    entries.Add(new("C-clip", 1, "Investigation notes: no active notes captured"));
            }

            return entries;
        }

        private static string ApplyPrivacyRedaction(ManifestEntry entry)
        {
            return entry.IsPrivate ? "[REDACTED]" : entry.Name;
        }

        private static ProsecutorBundleRevision CreateBundleRevision(ProsecutorBundle bundle, int userId, string? reason)
        {
            var manifest = (bundle.Manifest ?? Enumerable.Empty<ProsecutorBundleItem>())
                .OrderBy(item => item.Section)
                .ThenBy(item => item.Order)
                .Select(item => new { item.Section, item.Order, item.Name, item.IsPrivate, item.PrivacyReason });
            return new ProsecutorBundleRevision
            {
                FK_BundleID = bundle.BundleID,
                Version = bundle.Version,
                Title = bundle.Title,
                Notes = bundle.Notes,
                AISummaryText = bundle.AISummaryText,
                Status = bundle.Status,
                ManifestJson = JsonSerializer.Serialize(manifest),
                Reason = reason,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserID = userId
            };
        }

        private static object BundleResponse(ProsecutorBundle bundle)
        {
            return new
            {
                bundle.BundleID,
                bundle.FK_DocketID,
                bundle.Title,
                bundle.Notes,
                bundle.Status,
                bundle.AISummaryText,
                bundle.Version,
                bundle.CreatedAt,
                bundle.CreatedByUserID,
                bundle.UpdatedAt,
                bundle.UpdatedByUserID,
                bundle.ExportedAt,
                bundle.ExportedByUserID,
                bundle.ManifestPath,
                bundle.RecipientProsecutorUserID,
                bundle.SharedAt,
                bundle.SharedByUserID,
                bundle.ShareNote,
                bundle.SharedManifestHash,
                Manifest = (bundle.Manifest ?? Enumerable.Empty<ProsecutorBundleItem>())
                    .OrderBy(item => item.Section)
                    .ThenBy(item => item.Order)
                    .Select(item => new { item.ItemID, item.Section, item.Order, item.Name, item.IsPrivate, item.PrivacyReason })
                    .ToList(),
                Revisions = (bundle.Revisions ?? Enumerable.Empty<ProsecutorBundleRevision>())
                    .OrderByDescending(revision => revision.Version)
                    .Select(revision => new
                    {
                        revision.RevisionID,
                        revision.Version,
                        revision.Title,
                        revision.Notes,
                        revision.AISummaryText,
                        revision.Status,
                        revision.Reason,
                        revision.CreatedAt,
                        revision.CreatedByUserID
                    }).ToList()
            };
        }

        private static string SafeZipName(string value, string fallback)
        {
            var cleaned = string.Concat((value ?? string.Empty)
                .Select(character => Path.GetInvalidFileNameChars().Contains(character) || character is '/' or '\\' ? '_' : character))
                .Trim();
            if (string.IsNullOrWhiteSpace(cleaned)) cleaned = fallback;
            return cleaned.Length <= 120 ? cleaned : cleaned[..120];
        }

        private async Task AddProtectedFileToArchiveAsync(
            ZipArchive archive,
            string entryName,
            string? storedPath,
            string? expectedHash,
            List<string> omittedFiles)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return;
            try
            {
                var bytes = await _fileStorage.GetFileAsync(storedPath);
                if (!string.IsNullOrWhiteSpace(expectedHash))
                {
                    var actualHash = Convert.ToBase64String(SHA256.HashData(bytes));
                    if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(actualHash), Encoding.UTF8.GetBytes(expectedHash)))
                    {
                        omittedFiles.Add($"{entryName}: integrity check failed");
                        return;
                    }
                }

                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var target = entry.Open();
                await target.WriteAsync(bytes);
            }
            catch (FileNotFoundException)
            {
                omittedFiles.Add($"{entryName}: source file is unavailable");
            }
        }

        private int GetCurrentUserId()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "userId" || c.Type == "UserId" || c.Type == "UserID" || c.Type == "sub");
            if (claim == null) throw new UnauthorizedAccessException("User id claim not found");
            return int.Parse(claim.Value);
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

            var latestAssignment = docket.Investigation?.InvestigationAssignments?
                .OrderByDescending(assignment => assignment.DateAssigned)
                .ThenByDescending(assignment => assignment.AssignmentID)
                .FirstOrDefault();
            return !string.Equals(requester.Role?.Name, "Investigating Officer", StringComparison.OrdinalIgnoreCase) ||
                   (latestAssignment?.FK_UserID == userId &&
                    !string.Equals(latestAssignment.AllocationReviewStatus, "Unallocated", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> CanAccessBundleAsync(int bundleId, int userId)
        {
            var requester = await _db.Users.Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.UserID == userId);
            if (requester == null) return false;
            var bundle = await _db.ProsecutorBundles
                .Where(bundle => bundle.BundleID == bundleId)
                .Select(bundle => new { bundle.FK_DocketID, bundle.RecipientProsecutorUserID, bundle.Status })
                .FirstOrDefaultAsync();
            if (bundle == null) return false;
            if (string.Equals(requester.Role?.Name, "Prosecutor", StringComparison.OrdinalIgnoreCase))
                return bundle.RecipientProsecutorUserID == userId &&
                       string.Equals(bundle.Status, "Shared", StringComparison.OrdinalIgnoreCase);
            return await CanAccessDocketAsync(bundle.FK_DocketID, userId);
        }

        [HttpPatch("api/docket/{docketId}/assignment/accept")]
        [Authorize(Roles = "Investigating Officer")]
        public async Task<IActionResult> AcceptAssignment(int docketId)
        {
            var userId = GetCurrentUserId();
            var docket = await _db.Dockets.Include(d => d.Investigation)
                                          .ThenInclude(i => i.InvestigationAssignments)
                                          .Include(d => d.DocketStatus)
                                          .FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound();

            if (!string.Equals(docket.DocketStatus?.StatusName, "ALLOCATED", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Docket must be in ALLOCATED status to accept assignment.");

            var assignment = docket.Investigation?.InvestigationAssignments?
                .Where(a => a.FK_UserID == userId)
                .OrderByDescending(a => a.DateAssigned)
                .FirstOrDefault();
            if (assignment == null) return BadRequest("No assignment found for current user on this docket.");
            if (assignment.DateAccepted.HasValue) return BadRequest("Assignment has already been accepted.");

            var acceptedAt = DateTime.UtcNow;
            var inspectionHours = await _config.GetIntAsync("INSPECTION_SLA_HOURS", 24);
            assignment.DateAccepted = acceptedAt;
            assignment.InspectionDueAt = acceptedAt.AddHours(inspectionHours);

            var activeTimer = await _db.DocketTimers.FirstOrDefaultAsync(t => t.FK_DocketID == docketId && !t.IsCompleted);
            if (activeTimer == null)
            {
                await _db.DocketTimers.AddAsync(new DocketTimer
                {
                    FK_DocketID = docketId,
                    StartTime = acceptedAt,
                    DurationLimit = assignment.InspectionDueAt,
                    IsCompleted = false
                });
            }
            else
            {
                activeTimer.StartTime = acceptedAt;
                activeTimer.DurationLimit = assignment.InspectionDueAt;
            }

            // Optionally update docket status to UNDER_INVESTIGATION if current mapping exists
            var underInv = await _db.DocketStatuses.FirstOrDefaultAsync(s => s.StatusName == "UNDER_INVESTIGATION");
            if (underInv != null)
            {
                var oldStatus = docket.DocketStatus?.StatusName;
                docket.FK_DocketStatusID = underInv.DocketStatusID;
                await _db.DocketStatusHistories.AddAsync(new DocketStatusHistory
                {
                    FK_DocketID = docket.DocketID,
                    FromStatus = oldStatus,
                    ToStatus = underInv.StatusName,
                    ChangedAt = DateTime.UtcNow,
                    ChangedByUserID = userId,
                    Reason = "Assignment accepted"
                });
            }

            await _db.SaveChangesAsync();

            await _audit.LogActionAsync("ASSIGNMENT_ACCEPT", "Docket", docketId, null, new { assignment.DateAccepted, assignment.InspectionDueAt }, userId, "Assignment accepted");

            return NoContent();
        }

        public class AssignmentRejectionDto
        {
            public string? Reason { get; set; }
        }

        [HttpPatch("api/docket/{docketId}/assignment/reject")]
        [Authorize(Roles = "Investigating Officer")]
        public async Task<IActionResult> RejectAssignment(int docketId, [FromBody] AssignmentRejectionDto dto)
        {
            var userId = GetCurrentUserId();
            var reason = dto?.Reason?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
                return BadRequest("Provide a rejection reason of up to 500 characters.");

            var docket = await _db.Dockets
                .Include(d => d.Investigation).ThenInclude(i => i.InvestigationAssignments)
                .Include(d => d.DocketStatus)
                .FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound();
            if (!string.Equals(docket.DocketStatus?.StatusName, "ALLOCATED", StringComparison.OrdinalIgnoreCase))
                return BadRequest("This assignment cannot be rejected because the docket is no longer in ALLOCATED status.");

            var assignment = docket.Investigation?.InvestigationAssignments?
                .OrderByDescending(a => a.DateAssigned)
                .FirstOrDefault(a => a.FK_UserID == userId);
            if (assignment == null)
                return BadRequest("This assignment cannot be rejected because the docket is not allocated to you.");
            if (assignment.DateAccepted.HasValue)
                return BadRequest("This assignment cannot be rejected because it has already been accepted.");

            assignment.AllocationReviewStatus = "Officer Rejected";
            assignment.AllocationReviewNotes = reason;
            var openStatus = await _db.DocketStatuses.FirstOrDefaultAsync(s => s.StatusName == "OPEN");
            if (openStatus == null) return BadRequest("OPEN docket status is not configured.");
            docket.FK_DocketStatusID = openStatus.DocketStatusID;
            docket.DateAllocated = null;

            await _db.Notifications.AddAsync(new Notification
            {
                Title = "Docket Assignment Rejected",
                Message = $"The assigned officer rejected docket {docket.DocketNumber}. Reason: {reason}",
                PriorityLevel = "HIGH",
                FK_UserID = assignment.FK_AssignedByUserID
            });
            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("ASSIGNMENT_REJECT", "Docket", docketId, null,
                new { assignment.FK_UserID, Reason = reason, Status = "OPEN" }, userId, reason);
            return Ok(new
            {
                success = true,
                message = "Assignment rejected successfully. The allocating DSC Supervisor has been notified. This rejected assignment cannot be reallocated."
            });
        }

        public class AllocationReviewDto
        {
            public string? Decision { get; set; }
            public int? InvestigatingOfficerUserID { get; set; }
            public string? Reason { get; set; }
        }

        public class ReassignDocketDto
        {
            public int InvestigatingOfficerUserID { get; set; }
            public string? Reason { get; set; }
        }

        [HttpPatch("api/docket/{docketId}/assignment/reassign")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> ReassignDocket(int docketId, [FromBody] ReassignDocketDto dto)
        {
            var supervisorId = GetCurrentUserId();
            var reason = dto?.Reason?.Trim() ?? string.Empty;
            if (reason.Length < 10 || reason.Length > 500)
                return BadRequest(new { success = false, message = "A reassignment reason of 10 to 500 characters is required." });

            var docket = await _db.Dockets
                .Include(item => item.DocketStatus)
                .Include(item => item.Investigation)
                    .ThenInclude(investigation => investigation.InvestigationAssignments)
                .FirstOrDefaultAsync(item => item.DocketID == docketId);
            if (docket == null)
                return NotFound(new { success = false, message = "Docket not found." });
            if (!await CanAccessDocketAsync(docketId, supervisorId))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "The docket is outside your authorised station scope." });
            if (docket.DocketStatus?.StatusName is not ("ALLOCATED" or "UNDER_INVESTIGATION"))
                return BadRequest(new { success = false, message = "Only an allocated or active investigation docket can be reassigned." });

            var currentAssignment = docket.Investigation?.InvestigationAssignments?
                .OrderByDescending(assignment => assignment.DateAssigned)
                .ThenByDescending(assignment => assignment.AssignmentID)
                .FirstOrDefault();
            if (currentAssignment == null)
                return BadRequest(new { success = false, message = "No current assignment exists for this docket." });
            if (string.Equals(currentAssignment.AllocationReviewStatus, "Officer Rejected", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentAssignment.AllocationReviewStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "A rejected docket assignment cannot be reallocated." });
            if (currentAssignment.FK_UserID == dto.InvestigatingOfficerUserID)
                return BadRequest(new { success = false, message = "Select a different Investigating Officer for reassignment." });

            var replacement = await _db.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.UserID == dto.InvestigatingOfficerUserID);
            if (replacement == null || replacement.Role?.Name != "Investigating Officer" ||
                !replacement.IsActive || !replacement.IsActivated || replacement.IsLocked ||
                !string.Equals(replacement.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "The replacement must be an active, activated and unlocked Investigating Officer." });
            if (replacement.FK_StationID != docket.FK_StationID)
                return BadRequest(new { success = false, message = "The replacement Investigating Officer must be from the docket station." });

            var maxActiveDockets = await GetMaxActiveDocketsPerOfficerAsync();
            var activeDocketCount = await GetOfficerActiveDocketCountAsync(replacement.UserID);
            if (activeDocketCount >= maxActiveDockets)
                return BadRequest(new { success = false, message = $"The selected officer already manages the configured maximum of {maxActiveDockets} active dockets." });

            var reassignedAt = DateTime.UtcNow;
            currentAssignment.AllocationReviewStatus = "Reassigned";
            currentAssignment.AllocationReviewNotes = reason;
            await _db.InvestigationAssignments.AddAsync(new InvestigationAssignment
            {
                Role = "PRIMARY_IO",
                DateAssigned = reassignedAt,
                FK_InvestigationID = currentAssignment.FK_InvestigationID,
                FK_UserID = replacement.UserID,
                FK_AssignedByUserID = supervisorId,
                AllocationReviewStatus = "Awaiting Officer Response",
                AllocationReviewNotes = reason
            });

            var allocatedStatus = await _db.DocketStatuses.FirstOrDefaultAsync(status => status.StatusName == "ALLOCATED");
            if (allocatedStatus == null)
                return BadRequest(new { success = false, message = "ALLOCATED docket status is not configured." });
            docket.FK_DocketStatusID = allocatedStatus.DocketStatusID;
            docket.DateAllocated = reassignedAt;

            await _db.Notifications.AddAsync(new Notification
            {
                Title = "Docket Reassigned",
                Message = $"Docket {docket.DocketNumber} has been reassigned to you. Please review and accept the assignment.",
                PriorityLevel = "HIGH",
                FK_UserID = replacement.UserID
            });
            await _db.Notifications.AddAsync(new Notification
            {
                Title = "Docket Reassigned",
                Message = $"Docket {docket.DocketNumber} was reassigned to another Investigating Officer. Reason: {reason}",
                PriorityLevel = "NORMAL",
                FK_UserID = currentAssignment.FK_UserID
            });

            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("REASSIGN_DOCKET", "Docket", docketId,
                new { OfficerUserID = currentAssignment.FK_UserID },
                new { OfficerUserID = replacement.UserID }, supervisorId, reason);

            return Ok(new { success = true, message = $"Docket reassigned to {replacement.FirstName} {replacement.LastName}." });
        }

        [HttpPatch("api/docket/{docketId}/assignment/review")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> ReviewAllocation(int docketId, [FromBody] AllocationReviewDto dto)
        {
            var decision = (dto?.Decision ?? string.Empty).Trim().ToUpperInvariant();
            if (decision is not ("APPROVE" or "CHANGE" or "REJECT"))
                return BadRequest(new { success = false, message = "Decision must be Approve, Change or Reject." });
            if (string.IsNullOrWhiteSpace(dto?.Reason) || dto.Reason.Trim().Length < 10)
                return BadRequest(new { success = false, message = "A review reason of at least 10 characters is required." });

            var reviewerId = GetCurrentUserId();
            var docket = await _db.Dockets
                .Include(d => d.DocketStatus)
                .Include(d => d.Investigation)
                    .ThenInclude(i => i.InvestigationAssignments)
                        .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound(new { success = false, message = "The docket no longer exists." });
            if (!await CanAccessDocketAsync(docketId, reviewerId))
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "You cannot review this allocation because the docket is outside your authorised station scope." });
            if (!string.Equals(docket.DocketStatus?.StatusName, "ALLOCATED", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Only an allocated docket can be reviewed." });

            var assignment = docket.Investigation?.InvestigationAssignments?
                .OrderByDescending(a => a.DateAssigned)
                .FirstOrDefault();
            if (assignment == null) return BadRequest(new { success = false, message = "No allocation exists for this docket." });
            if (!string.Equals(assignment.AllocationReviewStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "This allocation has already been reviewed." });
            var reviewedAt = DateTime.UtcNow;
            assignment.AllocationReviewedAt = reviewedAt;
            assignment.AllocationReviewedByUserID = reviewerId;
            assignment.AllocationReviewNotes = dto.Reason.Trim();

            object result;
            if (decision == "APPROVE")
            {
                assignment.AllocationReviewStatus = "Approved";
                result = new { Decision = "Approved", assignment.FK_UserID };
            }
            else if (decision == "REJECT")
            {
                assignment.AllocationReviewStatus = "Rejected";
                var openStatus = await _db.DocketStatuses.FirstOrDefaultAsync(s => s.StatusName == "OPEN");
                if (openStatus == null) return BadRequest(new { success = false, message = "OPEN docket status is not configured." });
                docket.FK_DocketStatusID = openStatus.DocketStatusID;
                docket.DateAllocated = null;
                result = new { Decision = "Rejected", assignment.FK_UserID };
            }
            else
            {
                if (!dto.InvestigatingOfficerUserID.HasValue)
                    return BadRequest(new { success = false, message = "A replacement Investigating Officer is required for a Change decision." });

                var replacement = await _db.Users
                    .Include(user => user.Role)
                    .FirstOrDefaultAsync(user => user.UserID == dto.InvestigatingOfficerUserID.Value);
                if (replacement == null || replacement.Role?.Name != "Investigating Officer" ||
                    !replacement.IsActive || !replacement.IsActivated || replacement.IsLocked ||
                    !string.Equals(replacement.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { success = false, message = "The replacement must be an active, activated and unlocked Investigating Officer." });
                if (replacement.FK_StationID != docket.FK_StationID && !User.IsInRole("System Administrator"))
                    return BadRequest(new { success = false, message = "The replacement Investigating Officer must be from the docket station." });

                var maxActiveDockets = await GetMaxActiveDocketsPerOfficerAsync();
                var activeDocketCount = await GetOfficerActiveDocketCountAsync(replacement.UserID);
                if (activeDocketCount >= maxActiveDockets)
                {
                    var replacementName = $"{replacement.FirstName} {replacement.LastName}".Trim();
                    return BadRequest(new
                    {
                        success = false,
                        message = $"{replacementName} cannot be allocated this docket because they already manage {activeDocketCount} active dockets, which is the configured maximum of {maxActiveDockets}."
                    });
                }

                assignment.AllocationReviewStatus = "Changed";
                var replacementAssignment = new InvestigationAssignment
                {
                    Role = "PRIMARY_IO",
                    DateAssigned = reviewedAt,
                    FK_InvestigationID = assignment.FK_InvestigationID,
                    FK_UserID = replacement.UserID,
                    FK_AssignedByUserID = reviewerId,
                    AllocationReviewStatus = "Approved",
                    AllocationReviewedAt = reviewedAt,
                    AllocationReviewedByUserID = reviewerId,
                    AllocationReviewNotes = dto.Reason.Trim()
                };
                await _db.InvestigationAssignments.AddAsync(replacementAssignment);
                await _db.Notifications.AddAsync(new Notification
                {
                    Title = "Docket Allocation Confirmed",
                    Message = $"Supervisor review allocated docket {docket.DocketNumber} to you.",
                    PriorityLevel = "HIGH",
                    FK_UserID = replacement.UserID
                });
                result = new { Decision = "Changed", ReplacedUserID = assignment.FK_UserID, replacement.UserID };
            }

            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("REVIEW_DOCKET_ALLOCATION", "Docket", docketId, null, result,
                reviewerId, dto.Reason.Trim());

            return Ok(new { success = true, decision, message = $"Allocation review recorded: {decision}." });
        }

        public class InspectionCompleteDto
        {
            public string? Notes { get; set; }
        }

        [HttpPatch("api/docket/{docketId}/inspection/complete")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> CompleteInspection(int docketId, [FromBody] InspectionCompleteDto dto)
        {
            var userId = GetCurrentUserId();
            var docket = await _db.Dockets.Include(d => d.Investigation)
                                          .ThenInclude(i => i.InvestigationAssignments)
                                          .FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            var assignment = docket.Investigation?.InvestigationAssignments?.FirstOrDefault(a => a.DateAccepted.HasValue);
            if (assignment == null) return BadRequest("Assignment must have been accepted before inspection can be completed.");

            assignment.InspectionCompletedAt = DateTime.UtcNow;
            assignment.InspectionNotes = dto?.Notes;
            assignment.InspectionCompletedByUserID = userId;

            var activeTimer = await _db.DocketTimers.FirstOrDefaultAsync(t => t.FK_DocketID == docketId && !t.IsCompleted);
            if (activeTimer != null)
            {
                activeTimer.IsCompleted = true;
            }

            await _db.SaveChangesAsync();

            await _audit.LogActionAsync("INSPECTION_COMPLETE", "Docket", docketId, null, new { assignment.InspectionCompletedAt, assignment.InspectionNotes }, userId, dto?.Notes);

            return NoContent();
        }

        public class ChangeStatusDto
        {
            public string? Status { get; set; }
            public string? Reason { get; set; }
        }

        [HttpPatch("api/docket/{docketId}/status")]
        [Authorize(Roles = "Investigating Officer,DSC Supervisor,Docket Administrator")]
        public async Task<IActionResult> ChangeStatus(int docketId, [FromBody] ChangeStatusDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Status))
                return BadRequest("Status is required.");
            if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length < 10)
                return BadRequest("A reason of at least 10 characters is required.");

            var targetStatusName = dto.Status.Trim().ToUpperInvariant();
            var reason = dto.Reason.Trim();

            var userId = GetCurrentUserId();
            var docket = await _db.Dockets.Include(d => d.DocketStatus).FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            var targetStatus = await _db.DocketStatuses.FirstOrDefaultAsync(s => s.StatusName == targetStatusName);
            if (targetStatus == null) return BadRequest("Unknown target status.");

            var current = (docket.DocketStatus?.StatusName ?? string.Empty).ToUpperInvariant();

            // Allocation, acceptance, court outcome, close, archive and reopen all have
            // dedicated endpoints with their own validation and audit requirements.
            var valid = current switch
            {
                "UNDER_INVESTIGATION" => targetStatusName == "PENDING_PROSECUTOR",
                "PENDING_PROSECUTOR" => targetStatusName == "UNDER_INVESTIGATION",
                "REOPENED" => targetStatusName is "UNDER_INVESTIGATION" or "PENDING_PROSECUTOR",
                _ => false
            };

            if (!valid)
                return BadRequest($"Invalid status transition from {current} to {targetStatusName}. Use the dedicated workflow action where applicable.");

            var oldStatus = current;
            docket.FK_DocketStatusID = targetStatus.DocketStatusID;

            await _db.DocketStatusHistories.AddAsync(new DocketStatusHistory
            {
                FK_DocketID = docketId,
                FromStatus = oldStatus,
                ToStatus = targetStatus.StatusName,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserID = userId,
                Reason = reason
            });

            await _db.SaveChangesAsync();

            await _audit.LogActionAsync("STATUS_CHANGE", "Docket", docketId, oldStatus, targetStatus.StatusName, userId, reason);

            return NoContent();
        }

        // Prosecutor preview (privacy-aware)
        [HttpGet("api/docket/{docketId}/prosecutor-preview")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> ProsecutorPreview(int docketId)
        {
            var userId = GetCurrentUserId();
            var docket = await _db.Dockets
                .Include(d => d.DocketPrivacyFlags)
                .Include(d => d.Evidences)
                .Include(d => d.Incident)
                    .ThenInclude(i => i.IncidentType)
                .Include(d => d.Station)
                .FirstOrDefaultAsync(d => d.DocketID == docketId);

            if (docket == null) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            var hasActivePrivacyFlag = HasActiveDocketPrivacyFlag(docket);
            var disclosed = new Dictionary<string, object?>
            {
                ["DocketNumber"] = docket.DocketNumber,
                ["OBReferenceNumber"] = docket.OBReferenceNumber,
                ["FICReferenceNumber"] = docket.FICReferenceNumber,
                ["CrimeCategory"] = docket.Incident?.IncidentType?.TypeName,
                ["Station"] = docket.Station?.StationName,
                ["Jurisdiction"] = string.Join(", ", new[] { docket.Station?.Region, docket.Station?.Province }.Where(v => !string.IsNullOrWhiteSpace(v))),
                ["Summary"] = hasActivePrivacyFlag ? "[REDACTED]" : docket.DocketSummary
            };

            var manifest = BuildManifestEntries(docket)
                .Select(e => new
                {
                    e.Section,
                    e.Order,
                    Name = ApplyPrivacyRedaction(e),
                    e.IsPrivate,
                    e.PrivacyReason
                })
                .ToList();

            return Ok(new { disclosedFields = disclosed, manifest });
        }

        public class CreateBundleDto
        {
            public string? Title { get; set; }
            public string? Notes { get; set; }
        }

        [HttpPost("api/docket/{docketId}/prosecutor-bundles")]
        [Authorize(Roles = "Investigating Officer")]
        public async Task<IActionResult> CreateProsecutorBundle(int docketId, [FromBody] CreateBundleDto dto)
        {
            var userId = GetCurrentUserId();
            var docket = await _db.Dockets
                .Include(d => d.DocketPrivacyFlags)
                .Include(d => d.Evidences)
                .Include(d => d.Investigation)
                    .ThenInclude(i => i.InvestigationNotes)
                .FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            var bundle = new ProsecutorBundle
            {
                FK_DocketID = docketId,
                Title = dto?.Title,
                Notes = dto?.Notes,
                Status = "Draft",
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserID = userId
            };

            await _db.ProsecutorBundles.AddAsync(bundle);
            await _db.SaveChangesAsync();

            var items = BuildManifestEntries(docket)
                .Select(entry => new ProsecutorBundleItem
                {
                    FK_BundleID = bundle.BundleID,
                    Section = entry.Section,
                    Order = entry.Order,
                    Name = entry.Name,
                    IsPrivate = entry.IsPrivate,
                    PrivacyReason = entry.PrivacyReason
                }).ToList();

            await _db.ProsecutorBundleItems.AddRangeAsync(items);
            bundle.Manifest = items;
            await _db.ProsecutorBundleRevisions.AddAsync(CreateBundleRevision(bundle, userId, "Initial bundle"));
            await _db.SaveChangesAsync();

            var created = new
            {
                bundle.BundleID,
                bundle.FK_DocketID,
                bundle.Title,
                bundle.Notes,
                bundle.Status,
                bundle.Version,
                bundle.CreatedAt,
                bundle.CreatedByUserID,
                Manifest = items.Select(item => new
                {
                    item.ItemID,
                    item.Section,
                    item.Order,
                    item.Name,
                    item.IsPrivate,
                    item.PrivacyReason
                }).ToList()
            };
            await _audit.LogActionAsync("PROSECUTOR_BUNDLE_CREATE", "ProsecutorBundle", bundle.BundleID, null, created, userId, dto?.Notes);

            return CreatedAtAction(nameof(GetProsecutorBundle), new { bundleId = bundle.BundleID }, created);
        }

        [HttpGet("api/docket/{docketId}/prosecutor-bundles")]
        [Authorize(Roles = "Investigating Officer,DSC Supervisor,Prosecutor")]
        public async Task<IActionResult> GetProsecutorBundlesForDocket(int docketId)
        {
            var userId = GetCurrentUserId();
            if (!await _db.Dockets.AnyAsync(docket => docket.DocketID == docketId)) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();
            var bundles = await _db.ProsecutorBundles
                .Include(b => b.Manifest)
                .Include(b => b.Revisions)
                .Where(b => b.FK_DocketID == docketId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var role = await _db.Users.Where(user => user.UserID == userId)
                .Select(user => user.Role!.Name).FirstOrDefaultAsync();
            if (string.Equals(role, "Prosecutor", StringComparison.OrdinalIgnoreCase))
                bundles = bundles.Where(bundle => bundle.RecipientProsecutorUserID == userId && bundle.Status == "Shared").ToList();

            return Ok(bundles.Select(BundleResponse).ToList());
        }

        [HttpGet("api/prosecutor-bundles/{bundleId}")]
        [Authorize(Roles = "Investigating Officer,DSC Supervisor,Prosecutor")]
        public async Task<IActionResult> GetProsecutorBundle(int bundleId)
        {
            var bundle = await _db.ProsecutorBundles
                .Include(b => b.Manifest)
                .Include(b => b.Revisions)
                .FirstOrDefaultAsync(b => b.BundleID == bundleId);

            if (bundle == null) return NotFound();
            if (!await CanAccessBundleAsync(bundleId, GetCurrentUserId())) return Forbid();

            return Ok(BundleResponse(bundle));
        }

        public class UpdateBundleDto
        {
            public string? Title { get; set; }
            public string? Notes { get; set; }
            public string? AISummaryText { get; set; }
            public bool? IncludeEvidence { get; set; }
            public bool? IncludeNotes { get; set; }
            public string? Reason { get; set; }
        }

        [HttpPut("api/prosecutor-bundles/{bundleId}")]
        [Authorize(Roles = "Investigating Officer")]
        public async Task<IActionResult> UpdateBundle(int bundleId, [FromBody] UpdateBundleDto dto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(dto?.Reason))
                return BadRequest("A reason is required to update a prosecutor bundle.");
            if ((dto.IncludeEvidence.HasValue || dto.IncludeNotes.HasValue)
                && dto.IncludeEvidence == false && dto.IncludeNotes == false)
                return BadRequest("A bundle must include at least one item.");

            var bundle = await _db.ProsecutorBundles
                .Include(item => item.Manifest)
                .Include(item => item.Docket)
                    .ThenInclude(docket => docket!.DocketPrivacyFlags)
                .Include(item => item.Docket)
                    .ThenInclude(docket => docket!.Evidences)
                .Include(item => item.Docket)
                    .ThenInclude(docket => docket!.Investigation)
                        .ThenInclude(investigation => investigation!.InvestigationNotes)
                .FirstOrDefaultAsync(item => item.BundleID == bundleId);
            if (bundle == null) return NotFound();
            if (!await CanAccessBundleAsync(bundleId, userId)) return Forbid();

            if (!string.Equals(bundle.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only Draft prosecutor bundles can be updated.");

            var old = new
            {
                bundle.Title,
                bundle.Notes,
                bundle.AISummaryText,
                IncludedSections = (bundle.Manifest ?? Enumerable.Empty<ProsecutorBundleItem>())
                    .Select(item => item.Section)
                    .Distinct()
                    .OrderBy(section => section)
                    .ToList()
            };

            var hasAnyRevision = await _db.ProsecutorBundleRevisions.AnyAsync(item => item.FK_BundleID == bundleId);
            if (!hasAnyRevision)
            {
                bundle.Version = Math.Max(bundle.Version, 1);
                await _db.ProsecutorBundleRevisions.AddAsync(CreateBundleRevision(bundle, userId, "Legacy bundle baseline"));
            }

            bundle.Title = dto.Title ?? bundle.Title;
            bundle.Notes = dto.Notes ?? bundle.Notes;
            bundle.AISummaryText = dto.AISummaryText ?? bundle.AISummaryText;
            var includeEvidence = dto.IncludeEvidence ?? (bundle.Manifest?.Any(item => item.Section == "B-clip") ?? false);
            var includeNotes = dto.IncludeNotes ?? (bundle.Manifest?.Any(item => item.Section == "C-clip") ?? false);
            if (!includeEvidence && !includeNotes)
                return BadRequest("A bundle must include at least one item.");

            if (bundle.Manifest?.Any() == true && bundle.Docket != null)
            {
                _db.ProsecutorBundleItems.RemoveRange(bundle.Manifest);
                var refreshedItems = BuildManifestEntries(bundle.Docket, includeEvidence, includeNotes)
                    .Select(entry => new ProsecutorBundleItem
                    {
                        FK_BundleID = bundle.BundleID,
                        Section = entry.Section,
                        Order = entry.Order,
                        Name = entry.Name,
                        IsPrivate = entry.IsPrivate,
                        PrivacyReason = entry.PrivacyReason
                    }).ToList();
                await _db.ProsecutorBundleItems.AddRangeAsync(refreshedItems);
                bundle.Manifest = refreshedItems;
            }
            bundle.UpdatedAt = DateTime.UtcNow;
            bundle.UpdatedByUserID = userId;
            bundle.Version = Math.Max(bundle.Version, 1) + 1;
            await _db.ProsecutorBundleRevisions.AddAsync(CreateBundleRevision(bundle, userId, dto.Reason.Trim()));
            await _db.SaveChangesAsync();

            await _audit.LogActionAsync("PROSECUTOR_BUNDLE_UPDATE", "ProsecutorBundle", bundleId, old, new
            {
                bundle.Title,
                bundle.Notes,
                bundle.AISummaryText,
                IncludeEvidence = includeEvidence,
                IncludeNotes = includeNotes
            }, userId, dto.Reason);

            return NoContent();
        }

        public class ExportBundleDto
        {
            public string? Format { get; set; } // PDF | ZIP
        }

        public class ShareBundleDto
        {
            public int ProsecutorUserID { get; set; }
            public string? Note { get; set; }
        }

        [HttpGet("api/docket/{docketId}/eligible-prosecutors")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> GetEligibleProsecutors(int docketId)
        {
            var userId = GetCurrentUserId();
            var docket = await _db.Dockets.AsNoTracking().FirstOrDefaultAsync(item => item.DocketID == docketId);
            if (docket == null) return NotFound(new { message = "The docket does not exist." });
            if (!await CanAccessDocketAsync(docketId, userId))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You cannot select a prosecutor because this docket is outside your station access." });

            var prosecutors = await _db.Users.AsNoTracking()
                .Where(user => user.FK_StationID == docket.FK_StationID && user.IsActive && !user.IsLocked &&
                               user.AccountStatus == "Active" && user.Role != null && user.Role.Name == "Prosecutor")
                .OrderBy(user => user.LastName).ThenBy(user => user.FirstName)
                .Select(user => new
                {
                    user.UserID,
                    Name = ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim(),
                    user.EmailAddress,
                    user.PersonnelNumber
                }).ToListAsync();
            return Ok(prosecutors);
        }

        [HttpGet("api/prosecutor-bundles/{bundleId}/share-preview")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> PreviewBundleShare(int bundleId)
        {
            var userId = GetCurrentUserId();
            var bundle = await _db.ProsecutorBundles.Include(item => item.Manifest)
                .FirstOrDefaultAsync(item => item.BundleID == bundleId);
            if (bundle == null) return NotFound(new { message = "The prosecutor bundle does not exist." });
            if (!await CanAccessBundleAsync(bundleId, userId))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You cannot preview this bundle because its docket is outside your station access." });
            if (!string.Equals(bundle.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "Only a Draft bundle can be previewed for sharing because shared bundles are immutable." });

            var disclosed = (bundle.Manifest ?? Enumerable.Empty<ProsecutorBundleItem>())
                .OrderBy(item => item.Section).ThenBy(item => item.Order)
                .Select(item => new { item.ItemID, item.Section, item.Order, Name = item.IsPrivate ? "[REDACTED]" : item.Name, item.IsPrivate, item.PrivacyReason })
                .ToList();
            await _audit.LogActionAsync("PROSECUTOR_BUNDLE_SHARE_PREVIEW", "ProsecutorBundle", bundleId, null,
                new { bundle.Version, DisclosedItemCount = disclosed.Count(item => !item.IsPrivate), RedactedItemCount = disclosed.Count(item => item.IsPrivate) }, userId);
            return Ok(new { bundle.BundleID, bundle.Title, bundle.Version, Manifest = disclosed });
        }

        [HttpPost("api/prosecutor-bundles/{bundleId}/share")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> ShareBundle(int bundleId, [FromBody] ShareBundleDto dto)
        {
            var userId = GetCurrentUserId();
            if (dto == null || dto.ProsecutorUserID <= 0)
                return BadRequest(new { message = "Select the prosecutor who must receive this bundle." });
            if ((dto.Note ?? string.Empty).Trim().Length > 500)
                return BadRequest(new { message = "The sharing note cannot exceed 500 characters." });

            var bundle = await _db.ProsecutorBundles
                .Include(item => item.Manifest)
                .Include(item => item.Docket)
                    .ThenInclude(docket => docket!.DocketStatus)
                .FirstOrDefaultAsync(item => item.BundleID == bundleId);
            if (bundle == null) return NotFound(new { message = "The prosecutor bundle does not exist." });
            if (!await CanAccessBundleAsync(bundleId, userId))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You cannot share this bundle because its docket is outside your station access." });
            if (!string.Equals(bundle.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "This bundle cannot be shared because only Draft bundles may enter the sharing workflow." });

            var prosecutor = await _db.Users.Include(user => user.Role).FirstOrDefaultAsync(user => user.UserID == dto.ProsecutorUserID);
            if (prosecutor == null || !prosecutor.IsActive || prosecutor.IsLocked || prosecutor.AccountStatus != "Active" ||
                !string.Equals(prosecutor.Role?.Name, "Prosecutor", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "The selected recipient cannot receive the bundle because they are not an active Prosecutor." });
            if (prosecutor.FK_StationID != bundle.Docket?.FK_StationID)
                return BadRequest(new { message = "The selected prosecutor cannot receive this bundle because they belong to a different station." });

            var canonicalManifest = JsonSerializer.Serialize((bundle.Manifest ?? Enumerable.Empty<ProsecutorBundleItem>())
                .OrderBy(item => item.Section).ThenBy(item => item.Order)
                .Select(item => new { item.Section, item.Order, Name = item.IsPrivate ? "[REDACTED]" : item.Name, item.IsPrivate }));
            var sharedAt = DateTime.UtcNow;
            bundle.Status = "Shared";
            bundle.RecipientProsecutorUserID = prosecutor.UserID;
            bundle.SharedAt = sharedAt;
            bundle.SharedByUserID = userId;
            bundle.ShareNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
            bundle.SharedManifestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalManifest)));
            bundle.Version = Math.Max(bundle.Version, 1) + 1;
            bundle.UpdatedAt = sharedAt;
            bundle.UpdatedByUserID = userId;
            await _db.ProsecutorBundleRevisions.AddAsync(CreateBundleRevision(bundle, userId, $"Shared with prosecutor {prosecutor.UserID}"));

            // A successful controlled share is the business event that moves the
            // docket into prosecutor handling. Users must not select this status
            // manually from a generic status list.
            var docket = bundle.Docket;
            var previousStatus = docket?.DocketStatus?.StatusName;
            var pendingProsecutor = await _db.DocketStatuses
                .FirstOrDefaultAsync(status => status.StatusName == "PENDING_PROSECUTOR");
            if (docket != null && pendingProsecutor != null &&
                !string.Equals(previousStatus, "PENDING_PROSECUTOR", StringComparison.OrdinalIgnoreCase))
            {
                docket.FK_DocketStatusID = pendingProsecutor.DocketStatusID;
                await _db.DocketStatusHistories.AddAsync(new DocketStatusHistory
                {
                    FK_DocketID = docket.DocketID,
                    FromStatus = previousStatus,
                    ToStatus = pendingProsecutor.StatusName,
                    ChangedAt = sharedAt,
                    ChangedByUserID = userId,
                    Reason = "Prosecutor bundle shared with the assigned prosecutor"
                });
            }
            await _db.Notifications.AddAsync(new Notification
            {
                FK_UserID = prosecutor.UserID,
                User = prosecutor,
                Title = "Prosecutor bundle assigned",
                Message = $"Bundle '{bundle.Title ?? $"Bundle {bundle.BundleID}"}' has been shared with you for read-only prosecution review.",
                PriorityLevel = "High",
                CreatedAt = sharedAt
            });
            await _db.SaveChangesAsync();

            var recipientName = $"{prosecutor.FirstName} {prosecutor.LastName}".Trim();
            var result = new
            {
                Message = $"Prosecutor bundle has been shared successfully with {recipientName}.",
                bundle.BundleID,
                bundle.Status,
                bundle.Version,
                bundle.RecipientProsecutorUserID,
                RecipientName = recipientName,
                bundle.SharedAt,
                bundle.SharedManifestHash,
                DocketStatus = pendingProsecutor?.StatusName ?? previousStatus
            };
            await _audit.LogActionAsync("PROSECUTOR_BUNDLE_SHARE", "ProsecutorBundle", bundleId, null, result, userId, bundle.ShareNote);
            return Ok(result);
        }

        [HttpPost("api/prosecutor-bundles/{bundleId}/export")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> ExportBundle(int bundleId, [FromBody] ExportBundleDto dto)
        {
            var userId = GetCurrentUserId();
            var format = (dto?.Format ?? "PDF").Trim().ToUpperInvariant();
            if (format is not ("PDF" or "ZIP"))
                return BadRequest("Export format must be PDF or ZIP.");

            var bundle = await _db.ProsecutorBundles
                .Include(b => b.Manifest)
                .Include(b => b.Docket)
                    .ThenInclude(d => d.DocketPrivacyFlags)
                .FirstOrDefaultAsync(b => b.BundleID == bundleId);
            if (bundle == null) return NotFound();
            if (!await CanAccessBundleAsync(bundleId, userId)) return Forbid();
            if (!string.Equals(bundle.Status, "Shared", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "The bundle cannot be exported because a DSC Supervisor must share it with a prosecutor first." });
            var activeDocketPrivacyFlag = bundle.Docket != null && HasActiveDocketPrivacyFlag(bundle.Docket);
            bool RedactItem(ProsecutorBundleItem item) =>
                item.IsPrivate ||
                (activeDocketPrivacyFlag &&
                 string.Equals(item.Section, "A-clip", StringComparison.OrdinalIgnoreCase) &&
                 item.Order == 3);

            var hasAnyRevision = await _db.ProsecutorBundleRevisions.AnyAsync(item => item.FK_BundleID == bundleId);
            if (!hasAnyRevision)
            {
                bundle.Version = Math.Max(bundle.Version, 1);
                await _db.ProsecutorBundleRevisions.AddAsync(CreateBundleRevision(bundle, userId, "Legacy bundle baseline"));
            }

            bundle.ExportedAt = DateTime.UtcNow;
            bundle.ExportedByUserID = userId;
            bundle.ManifestPath = $"/api/prosecutor-bundles/{bundleId}/download?format={format}";
            await _db.SaveChangesAsync();

            var exportMeta = new
            {
                bundle.BundleID,
                bundle.Title,
                bundle.Notes,
                bundle.Status,
                bundle.Version,
                Format = format,
                bundle.ManifestPath,
                bundle.ExportedAt,
                Manifest = bundle.Manifest?
                    .OrderBy(m => m.Section)
                    .ThenBy(m => m.Order)
                    .Select(m => new { m.Section, m.Order, Name = RedactItem(m) ? "[REDACTED]" : m.Name, IsPrivate = RedactItem(m), m.PrivacyReason })
            };

            await _audit.LogActionAsync("PROSECUTOR_BUNDLE_EXPORT", "ProsecutorBundle", bundleId, null, exportMeta, userId, $"Export requested: {dto?.Format}");

            return Ok(exportMeta);
        }

        [HttpGet("api/prosecutor-bundles/{bundleId}/download")]
        [Authorize(Roles = "DSC Supervisor,Prosecutor")]
        public async Task<IActionResult> DownloadBundle(int bundleId, [FromQuery] string format = "PDF")
        {
            var userId = GetCurrentUserId();
            var normalizedFormat = (format ?? "PDF").Trim().ToUpperInvariant();
            if (normalizedFormat is not ("PDF" or "ZIP"))
                return BadRequest("Export format must be PDF or ZIP.");

            var bundle = await _db.ProsecutorBundles
                .Include(b => b.Manifest)
                .Include(b => b.Docket)
                    .ThenInclude(d => d.DocketPrivacyFlags)
                .Include(b => b.Docket)
                    .ThenInclude(d => d.Evidences)
                .Include(b => b.Docket)
                    .ThenInclude(d => d.Investigation)
                        .ThenInclude(investigation => investigation.InvestigationNotes)
                            .ThenInclude(note => note.NoteAttachments)
                .FirstOrDefaultAsync(b => b.BundleID == bundleId);
            if (bundle == null) return NotFound();
            if (!await CanAccessBundleAsync(bundleId, userId)) return Forbid();
            if (!string.Equals(bundle.Status, "Shared", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "The bundle cannot be downloaded because it has not been shared with a prosecutor." });

            var activeDocketPrivacyFlag = bundle.Docket != null && HasActiveDocketPrivacyFlag(bundle.Docket);
            bool RedactItem(ProsecutorBundleItem item) =>
                item.IsPrivate ||
                (activeDocketPrivacyFlag &&
                 string.Equals(item.Section, "A-clip", StringComparison.OrdinalIgnoreCase) &&
                 item.Order == 3);

            var lines = new List<string>
            {
                $"Bundle ID: {bundle.BundleID}",
                $"Docket: {bundle.Docket?.DocketNumber ?? bundle.FK_DocketID.ToString()}",
                $"Status: {bundle.Status}",
                $"Version: {bundle.Version}",
                $"Notes: {bundle.Notes ?? "-"}",
                $"AI summary: {bundle.AISummaryText ?? "-"}",
                "Manifest:"
            };
            lines.AddRange((bundle.Manifest ?? Enumerable.Empty<ProsecutorBundleItem>())
                .OrderBy(item => item.Section)
                .ThenBy(item => item.Order)
                .Select(item => $"{item.Section} {item.Order}: {(RedactItem(item) ? "[REDACTED]" : item.Name)}"));

            // The official SAPS docket number remains the immutable reference inside the
            // document.  The human-facing case file is named for its complainant so an
            // exported bundle is immediately recognisable in a download folder.
            var complainantName = SafeZipName(
                bundle.Docket?.ComplainantFullName ?? string.Empty,
                bundle.Docket?.DocketNumber ?? $"Docket {bundle.FK_DocketID}");
            var documentTitle = $"{complainantName} - Prosecutor Bundle";
            var baseFileName = SafeZipName(documentTitle, $"Docket {bundle.FK_DocketID} - Prosecutor Bundle");
            await _audit.LogActionAsync("DOWNLOAD_PROSECUTOR_BUNDLE", "ProsecutorBundle", bundleId,
                null, new { Format = normalizedFormat }, userId);

            if (normalizedFormat == "PDF")
            {
                var generatedBy = await _db.Users
                    .Where(user => user.UserID == userId)
                    .Select(user => (user.FirstName + " " + user.LastName).Trim())
                    .FirstOrDefaultAsync() ?? "SAPS DDMS user";
                var pdf = _pdfService.BuildTextReport(documentTitle, generatedBy, lines);
                return File(pdf, "application/pdf", $"{baseFileName}.pdf");
            }

            await using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("manifest.txt", CompressionLevel.Optimal);
                await using (var entryStream = entry.Open())
                await using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                    await writer.WriteAsync(string.Join(Environment.NewLine, lines));

                var omittedFiles = new List<string>();
                var privateEvidenceOrders = (bundle.Manifest ?? Enumerable.Empty<ProsecutorBundleItem>())
                    .Where(item => string.Equals(item.Section, "B-clip", StringComparison.OrdinalIgnoreCase) && RedactItem(item))
                    .Select(item => item.Order)
                    .ToHashSet();
                var evidence = (bundle.Docket?.Evidences ?? Enumerable.Empty<Evidence>())
                    .OrderBy(item => item.EvidenceReferenceNumber)
                    .ThenBy(item => item.EvidenceID)
                    .ToList();
                for (var index = 0; index < evidence.Count; index++)
                {
                    var item = evidence[index];
                    var order = index + 1;
                    if (privateEvidenceOrders.Contains(order) || string.IsNullOrWhiteSpace(item.ImagePath)) continue;
                    var extension = Path.GetExtension(item.ImagePath);
                    var name = SafeZipName(item.EvidenceReferenceNumber ?? $"evidence_{item.EvidenceID}", $"evidence_{item.EvidenceID}");
                    await AddProtectedFileToArchiveAsync(archive, $"B-clip/evidence/{order:D3}_{name}{extension}",
                        item.ImagePath, item.FileHash, omittedFiles);
                }

                var privateNoteOrders = (bundle.Manifest ?? Enumerable.Empty<ProsecutorBundleItem>())
                    .Where(item => string.Equals(item.Section, "C-clip", StringComparison.OrdinalIgnoreCase) && RedactItem(item))
                    .Select(item => item.Order)
                    .ToHashSet();
                var notes = (bundle.Docket?.Investigation?.InvestigationNotes ?? Enumerable.Empty<InvestigationNote>())
                    .Where(note => !string.Equals(note.Status, "Archived", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(note => note.EntryDate)
                    .ThenBy(note => note.NoteID)
                    .ToList();
                for (var noteIndex = 0; noteIndex < notes.Count; noteIndex++)
                {
                    var order = noteIndex + 1;
                    if (privateNoteOrders.Contains(order)) continue;
                    foreach (var attachment in notes[noteIndex].NoteAttachments ?? Enumerable.Empty<NoteAttachment>())
                    {
                        var name = SafeZipName(attachment.FileName, $"attachment_{attachment.AttachmentID}");
                        await AddProtectedFileToArchiveAsync(archive, $"C-clip/notes/{order:D3}_{attachment.AttachmentID:D6}_{name}",
                            attachment.FilePath, attachment.Hash, omittedFiles);
                    }
                }

                if (omittedFiles.Count > 0)
                {
                    var omittedEntry = archive.CreateEntry("omitted-files.txt", CompressionLevel.Optimal);
                    await using var omittedStream = omittedEntry.Open();
                    await using var omittedWriter = new StreamWriter(omittedStream, Encoding.UTF8);
                    await omittedWriter.WriteAsync(string.Join(Environment.NewLine, omittedFiles));
                }
            }

            return File(output.ToArray(), "application/zip", $"{baseFileName}.zip");
        }

        [HttpPatch("api/prosecutor-bundles/{bundleId}/items/{itemId}/privacy-flag")]
        [Authorize(Roles = "Investigating Officer,DSC Supervisor")]
        public async Task<IActionResult> SetBundleItemPrivacyFlag(int bundleId, int itemId, [FromBody] PrivacyFlagDto dto)
        {
            var userId = GetCurrentUserId();
            var item = await _db.ProsecutorBundleItems
                .Include(i => i.ProsecutorBundle)
                    .ThenInclude(bundle => bundle!.Docket)
                        .ThenInclude(docket => docket!.Investigation)
                            .ThenInclude(investigation => investigation!.InvestigationAssignments)
                .FirstOrDefaultAsync(i => i.FK_BundleID == bundleId && i.ItemID == itemId);
            if (item == null) return NotFound();
            if (!await CanAccessBundleAsync(bundleId, userId)) return Forbid();

            var actingUser = await _db.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.UserID == userId);
            if (actingUser == null) return Unauthorized();

            var reason = (dto.Reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest(new { success = false, message = "Privacy flag reason is required." });
            if (reason.Length > 300)
                return BadRequest(new { success = false, message = "Privacy flag reason must not exceed 300 characters." });

            var roleName = actingUser.Role?.Name ?? string.Empty;
            var isSystemAdministrator = string.Equals(roleName, "System Administrator", StringComparison.OrdinalIgnoreCase);
            var isInvestigatingOfficer = string.Equals(roleName, "Investigating Officer", StringComparison.OrdinalIgnoreCase);
            var isDscSupervisor = string.Equals(roleName, "DSC Supervisor", StringComparison.OrdinalIgnoreCase);
            var activeFlagExists = item.IsPrivate;

            if (dto.IsPrivate)
            {
                if (activeFlagExists)
                {
                    if (isInvestigatingOfficer && !isSystemAdministrator)
                        return Conflict(new { success = false, message = "This item already has an active privacy flag. Use 'Manage Flag' to update or remove it." });

                    if (!isDscSupervisor && !isSystemAdministrator)
                        return Forbid();
                }
                else if (!isInvestigatingOfficer && !isSystemAdministrator)
                {
                    return Forbid();
                }
            }
            else
            {
                if (!activeFlagExists)
                    return BadRequest(new { success = false, message = "No active privacy flag found for this item." });

                if (!isDscSupervisor && !isSystemAdministrator)
                    return Forbid();
            }

            var old = new { item.IsPrivate, item.PrivacyReason };
            item.IsPrivate = dto.IsPrivate;
            item.PrivacyReason = reason;

            var docketId = item.ProsecutorBundle?.FK_DocketID ?? 0;
            if (docketId > 0)
            {
                await _db.PrivacyFlagHistories.AddAsync(new PrivacyFlagHistory
                {
                    FK_DocketID = docketId,
                    IsPrivate = dto.IsPrivate,
                    Reason = $"Bundle item {item.ItemID} ({item.Section} {item.Order} - {item.Name}): {reason}",
                    Timestamp = DateTime.UtcNow,
                    ChangedByUserID = userId
                });
            }

            await _db.SaveChangesAsync();
            var actionType = dto.IsPrivate
                ? activeFlagExists ? "UPDATE_PRIVACY_FLAG" : "CREATE_PRIVACY_FLAG"
                : "REMOVE_PRIVACY_FLAG";
            await _audit.LogActionAsync(actionType,
                "ProsecutorBundleItem", itemId, old, new { item.IsPrivate, item.PrivacyReason }, userId, reason);

            return NoContent();
        }

        // Privacy flag endpoints
        public class PrivacyFlagDto
        {
            public bool IsPrivate { get; set; }
            public string? Reason { get; set; }
        }

        [HttpGet("api/docket-privacy-flags/{flagId}/history")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> GetBundleItemPrivacyFlagHistory(int flagId)
        {
            var userId = GetCurrentUserId();
            var item = await _db.ProsecutorBundleItems
                .AsNoTracking()
                .Include(bundleItem => bundleItem.ProsecutorBundle)
                .FirstOrDefaultAsync(bundleItem => bundleItem.ItemID == flagId);
            if (item == null) return NotFound();
            if (!await CanAccessBundleAsync(item.FK_BundleID, userId)) return Forbid();

            var history = await _db.UserAuditLogs
                .AsNoTracking()
                .Where(log => log.AffectedEntity == "ProsecutorBundleItem" &&
                              log.AffectedEntityID == flagId &&
                              (log.ActionType == "CREATE_PRIVACY_FLAG" ||
                               log.ActionType == "UPDATE_PRIVACY_FLAG" ||
                               log.ActionType == "REMOVE_PRIVACY_FLAG"))
                .OrderBy(log => log.Timestamp)
                .Select(log => new
                {
                    HistoryId = log.UserAuditLogID,
                    FlagId = flagId,
                    Action = log.ActionType == "CREATE_PRIVACY_FLAG"
                        ? "Created"
                        : log.ActionType == "UPDATE_PRIVACY_FLAG"
                            ? "Updated"
                            : "Removed",
                    PerformedByUserID = log.FK_UserID,
                    PerformedBy = log.User != null
                        ? ((log.User.FirstName ?? string.Empty) + " " + (log.User.LastName ?? string.Empty)).Trim()
                        : string.Empty,
                    PerformedAt = log.Timestamp,
                    Notes = log.Reason
                })
                .ToListAsync();

            return Ok(new
            {
                ItemId = item.ItemID,
                ItemName = item.Name,
                History = history
            });
        }

        [HttpPatch("api/docket/{docketId}/privacy-flag")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> SetPrivacyFlag(int docketId, [FromBody] PrivacyFlagDto dto)
        {
            var userId = GetCurrentUserId();
            var docket = await _db.Dockets.Include(d => d.DocketPrivacyFlags).FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            if (dto.IsPrivate)
            {
                var flag = new DocketPrivacyFlag
                {
                    FK_DocketID = docketId,
                    FieldName = "DocketSummary",
                    Reason = dto.Reason,
                    AppliedAt = DateTime.UtcNow,
                    FK_Applied_By_UserID = userId,
                    FK_Applied_For_UserID = userId
                };
                await _db.DocketPrivacyFlags.AddAsync(flag);
                await _db.PrivacyFlagHistories.AddAsync(new PrivacyFlagHistory
                {
                    FK_DocketID = docketId,
                    IsPrivate = true,
                    Reason = dto.Reason,
                    Timestamp = DateTime.UtcNow,
                    ChangedByUserID = userId
                });
            }
            else
            {
                var active = docket.DocketPrivacyFlags?.FirstOrDefault(f => f.RemovedAt == null);
                if (active != null)
                {
                    active.RemovedAt = DateTime.UtcNow;
                    active.RemovalReason = dto.Reason;
                    active.FK_Removed_By_UserID = userId;
                    await _db.PrivacyFlagHistories.AddAsync(new PrivacyFlagHistory
                    {
                        FK_DocketID = docketId,
                        IsPrivate = false,
                        Reason = dto.Reason,
                        Timestamp = DateTime.UtcNow,
                        ChangedByUserID = userId
                    });
                }
            }

            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("PRIVACY_FLAG_UPDATE", "Docket", docketId, null, dto, userId, dto.Reason);
            return NoContent();
        }

        [HttpGet("api/docket/{docketId}/privacy-flag/history")]
        [Authorize(Roles = "DSC Supervisor,Docket Administrator")]
        public async Task<IActionResult> GetPrivacyFlagHistory(int docketId)
        {
            var userId = GetCurrentUserId();
            if (!await _db.Dockets.AnyAsync(docket => docket.DocketID == docketId)) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();
            var history = await _db.PrivacyFlagHistories.Where(h => h.FK_DocketID == docketId).OrderBy(h => h.Timestamp).ToListAsync();
            return Ok(history);
        }

        // ── UC 3.4 — Archive Docket ──────────────────────────────────────────
        [HttpPatch("api/docket/{docketId}/archive")]
        [Authorize(Roles = "Docket Administrator")]
        public async Task<IActionResult> ArchiveDocket(int docketId)
        {
            var userId = GetCurrentUserId();
            var docket = await _db.Dockets.Include(d => d.DocketStatus).FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            if (!string.Equals(docket.DocketStatus?.StatusName, "CLOSED", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only CLOSED dockets can be archived.");

            // Check all linked investigation notes are Approved or Archived
            var pendingNotes = await _db.InvestigationNotes
                .Where(n => n.FK_InvestigationID == docket.FK_InvestigationID
                    && n.Status != "Approved" && n.Status != "Archived")
                .CountAsync();

            if (pendingNotes > 0)
                return Conflict("Cannot archive docket. One or more investigation notes are awaiting approval.");

            var archived = await _db.DocketStatuses.FirstOrDefaultAsync(s => s.StatusName == "ARCHIVED");
            if (archived == null) return StatusCode(500, "ARCHIVED status not found.");

            var oldStatus = docket.DocketStatus?.StatusName;
            docket.FK_DocketStatusID = archived.DocketStatusID;
            docket.DocketArchivedAt = DateTime.UtcNow;

            await _db.DocketStatusHistories.AddAsync(new DocketStatusHistory
            {
                FK_DocketID = docketId,
                FromStatus = oldStatus,
                ToStatus = "ARCHIVED",
                ChangedAt = DateTime.UtcNow,
                ChangedByUserID = userId,
                Reason = "Docket archived"
            });

            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("ARCHIVE_DOCKET", "Docket", docketId, oldStatus, "ARCHIVED", userId, "Docket archived");

            return NoContent();
        }

        // ── UC 8.1 — Record Court Outcome ────────────────────────────────────
        public class CourtOutcomeDto
        {
            public string? Outcome { get; set; }
            public DateTime CourtDate { get; set; }
            public string? PresidingOfficer { get; set; }
            public string? Sentence { get; set; }
            public string? Notes { get; set; }
        }

        [HttpPost("api/docket/{docketId}/court-outcome")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> RecordCourtOutcome(int docketId, [FromBody] CourtOutcomeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Outcome)) return BadRequest("Court outcome is required.");
            if (dto.CourtDate == default) return BadRequest("A valid court date is required.");
            if (dto.CourtDate.Date > DateTime.UtcNow.Date) return BadRequest("Court date cannot be in the future.");
            if (dto.Outcome == "Guilty" && string.IsNullOrWhiteSpace(dto.Sentence))
                return BadRequest("Sentence details are required for a Guilty outcome.");

            var userId = GetCurrentUserId();
            var docket = await _db.Dockets.Include(d => d.DocketStatus).FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();
            if (dto.CourtDate.Date < docket.DateCreated.Date)
                return BadRequest("Court date cannot be earlier than the date the docket was created.");

            var hasSharedBundle = await _db.ProsecutorBundles
                .AnyAsync(b => b.FK_DocketID == docketId && b.Status == "Shared" && b.RecipientProsecutorUserID.HasValue);
            if (!hasSharedBundle)
                return BadRequest("A prosecutor bundle must be shared with an assigned prosecutor before a court outcome can be recorded.");

            // Insert CourtAppearance record using ERD table name
            var appearance = new CourtAppearance
            {
                FK_DocketID = docketId,
                AppearanceDate = dto.CourtDate.Date,
                CourtroomName = string.Empty,
                PresidingOfficer = dto.PresidingOfficer ?? string.Empty,
                AppearanceReason = dto.Outcome,
                Outcome = dto.Outcome,
                Sentence = dto.Sentence?.Trim(),
                Notes = dto.Notes
            };
            await _db.CourtAppearances.AddAsync(appearance);

            // Update docket status to COURT_OUTCOME_RECORDED
            var courtStatus = await _db.DocketStatuses.FirstOrDefaultAsync(s => s.StatusName == "COURT_OUTCOME_RECORDED")
                ?? await _db.DocketStatuses.FirstOrDefaultAsync(s => s.StatusName == "CLOSED");

            if (courtStatus != null)
            {
                var oldStatus = docket.DocketStatus?.StatusName;
                docket.FK_DocketStatusID = courtStatus.DocketStatusID;
                await _db.DocketStatusHistories.AddAsync(new DocketStatusHistory
                {
                    FK_DocketID = docketId,
                    FromStatus = oldStatus,
                    ToStatus = courtStatus.StatusName,
                    ChangedAt = DateTime.UtcNow,
                    ChangedByUserID = userId,
                    Reason = $"Court outcome recorded: {dto.Outcome}"
                });
            }

            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("RECORD_COURT_OUTCOME", "CourtAppearance", appearance.CourtAppearanceID, null, dto, userId, dto.Outcome);

            return Ok(new
            {
                success = true,
                message = "Court outcome recorded successfully.",
                courtAppearanceId = appearance.CourtAppearanceID,
                docketId,
                status = courtStatus?.StatusName ?? docket.DocketStatus?.StatusName,
                outcome = appearance.Outcome,
                appearanceDate = appearance.AppearanceDate
            });
        }

        [HttpPatch("api/docket/{docketId}/close")]
        [Authorize(Roles = "DSC Supervisor,Docket Administrator")]
        public async Task<IActionResult> CloseCase(int docketId, [FromBody] ChangeStatusDto dto)
        {
            var userId = GetCurrentUserId();
            var docket = await _db.Dockets
                .Include(d => d.DocketStatus)
                .Include(d => d.Investigation)
                .FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            if (!string.Equals(docket.DocketStatus?.StatusName, "COURT_OUTCOME_RECORDED", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only dockets with a recorded court outcome can be closed.");

            var closed = await _db.DocketStatuses.FirstOrDefaultAsync(s => s.StatusName == "CLOSED");
            if (closed == null) return StatusCode(500, "CLOSED status not found.");

            var oldStatus = docket.DocketStatus?.StatusName;
            docket.FK_DocketStatusID = closed.DocketStatusID;
            docket.DocketClosedAt = DateTime.UtcNow;
            if (docket.Investigation != null)
            {
                docket.Investigation.EndDate = DateTime.UtcNow;
                var completed = await _db.InvestigationStatuses.FirstOrDefaultAsync(s => s.StatusName == "COMPLETED");
                if (completed != null)
                    docket.Investigation.FK_InvestigationStatusID = completed.InvestigationStatusID;
            }

            await _db.DocketStatusHistories.AddAsync(new DocketStatusHistory
            {
                FK_DocketID = docketId,
                FromStatus = oldStatus,
                ToStatus = "CLOSED",
                ChangedAt = DateTime.UtcNow,
                ChangedByUserID = userId,
                Reason = dto?.Reason ?? "Case closed"
            });

            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("CLOSE_CASE", "Docket", docketId, oldStatus, "CLOSED", userId, dto?.Reason);

            return NoContent();
        }

        [HttpPatch("api/docket/{docketId}/reopen")]
        [Authorize(Roles = "DSC Supervisor,Docket Administrator")]
        public async Task<IActionResult> ReopenCase(int docketId, [FromBody] ChangeStatusDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Reason) || dto.Reason.Trim().Length < 10)
                return BadRequest("A reopening reason of at least 10 characters is required.");

            var userId = GetCurrentUserId();
            var docket = await _db.Dockets.Include(d => d.DocketStatus).FirstOrDefaultAsync(d => d.DocketID == docketId);
            if (docket == null) return NotFound();
            if (!await CanAccessDocketAsync(docketId, userId)) return Forbid();

            if (!string.Equals(docket.DocketStatus?.StatusName, "CLOSED", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(docket.DocketStatus?.StatusName, "ARCHIVED", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only closed or archived dockets can be reopened.");

            var reopened = await _db.DocketStatuses.FirstOrDefaultAsync(s => s.StatusName == "REOPENED")
                ?? await _db.DocketStatuses.FirstOrDefaultAsync(s => s.StatusName == "OPEN");
            if (reopened == null) return StatusCode(500, "REOPENED status not found.");

            var oldStatus = docket.DocketStatus?.StatusName;
            docket.FK_DocketStatusID = reopened.DocketStatusID;
            docket.ReopenedAt = DateTime.UtcNow;
            await _db.DocketStatusHistories.AddAsync(new DocketStatusHistory
            {
                FK_DocketID = docketId,
                FromStatus = oldStatus,
                ToStatus = reopened.StatusName,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserID = userId,
                Reason = dto.Reason
            });

            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("REOPEN_CASE", "Docket", docketId, oldStatus, reopened.StatusName, userId, dto.Reason);

            return NoContent();
        }
    }
}
