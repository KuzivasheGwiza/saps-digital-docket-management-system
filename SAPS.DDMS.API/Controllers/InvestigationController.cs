// Controllers/InvestigationController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Services;
using System.Security.Claims;

using static SAPS.DDMS.API.Models.DTOs.InvestigationNoteDtos;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Investigating Officer,DSC Supervisor,Docket Administrator")]
    public class InvestigationController : ControllerBase
    {
        private readonly IInvestigationNoteService _noteService;
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public InvestigationController(IInvestigationNoteService noteService, ApplicationDbContext db, IAuditService auditService)
        {
            _noteService = noteService;
            _db = db;
            _auditService = auditService;
        }

        /// <summary>
        /// UC 4.2 - Get investigation notes by investigation ID
        /// </summary>
        [HttpGet("{investigationId}/notes")]
        public async Task<IActionResult> GetInvestigationNotes(int investigationId)
        {
            var userId = GetCurrentUserId();
            var result = await _noteService.GetNotesByInvestigationAsync(investigationId, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            var notes = result.Data.Select(note => new InvestigationNoteResponseDto
            {
                NoteID = note.NoteID,
                Description = note.Description,
                EntryDate = note.EntryDate,
                Status = note.Status,
                AuthorName = $"{note.Author?.FirstName} {note.Author?.LastName}".Trim(),
                AuthorUserID = note.FK_AuthorUserID,
                Attachments = note.NoteAttachments?.Select(attachment => new NoteAttachmentDto
                {
                    AttachmentID = attachment.AttachmentID,
                    FileName = attachment.FileName,
                    FileSize = attachment.FileSize,
                    DownloadUrl = $"/api/files/note-attachments/{attachment.AttachmentID}"
                }).ToList() ?? new List<NoteAttachmentDto>()
            }).ToList();

            return Ok(notes);
        }

        /// <summary>
        /// UC 4.1 - Create investigation note
        /// </summary>
        [HttpPost("notes")]
        [Authorize(Roles = "Investigating Officer")]
        public async Task<IActionResult> CreateInvestigationNote([FromForm] CreateInvestigationNoteDto createDto)
        {
            var userId = GetCurrentUserId();
            var result = await _noteService.CreateNoteAsync(createDto.InvestigationID, createDto.Description, userId, createDto.Attachment);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message, noteId = result.Data?.NoteID });
        }

        /// <summary>
        /// UC 4.3 - Update investigation note
        /// Requires reason (min 10 characters)
        /// </summary>
        [HttpPut("notes/{noteId}")]
        [Authorize(Roles = "Investigating Officer, Docket Administrator")]
        public async Task<IActionResult> UpdateInvestigationNote(int noteId, [FromBody] UpdateInvestigationNoteDto updateDto)
        {
            var userId = GetCurrentUserId();
            var result = await _noteService.UpdateNoteAsync(noteId, updateDto.Description, updateDto.Reason, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Get note version history
        /// </summary>
        [HttpGet("notes/{noteId}/versions")]
        [Authorize(Roles = "Investigating Officer, Docket Administrator, DSC Supervisor")]
        public async Task<IActionResult> GetNoteVersions(int noteId)
        {
            var userId = GetCurrentUserId();
            var result = await _noteService.GetNoteVersionsAsync(noteId, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            var versions = result.Data.Select(version => new
            {
                version.VersionID,
                version.VersionNumber,
                version.NoteTextBefore,
                version.NoteTextAfter,
                version.EditedAt,
                version.Reason,
                EditedBy = $"{version.User?.FirstName} {version.User?.LastName}".Trim()
            }).ToList();

            return Ok(versions);
        }

        public class ArchiveNoteRequestDto
        {
            public string Reason { get; set; } = string.Empty;
        }

        [HttpPost("notes/{noteId}/archive-request")]
        [Authorize(Roles = "Investigating Officer")]
        public async Task<IActionResult> RequestArchiveNote(int noteId, [FromBody] ArchiveNoteRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Reason) || dto.Reason.Trim().Length < 10)
                return BadRequest(new { success = false, message = "Archive reason must be at least 10 characters." });

            var userId = GetCurrentUserId();
            var note = await _db.InvestigationNotes.FindAsync(noteId);
            if (note == null) return NotFound(new { success = false, message = "Investigation note not found." });
            if (note.Status == "Archived")
                return BadRequest(new { success = false, message = "Archived notes cannot be requested again." });

            var requester = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == userId);
            if (requester == null) return Unauthorized();
            var isSystemAdmin = requester?.Role?.Name == "System Administrator";
            var isAuthor = note.FK_AuthorUserID == userId;
            var isAssigned = await _db.InvestigationAssignments
                .AnyAsync(a => a.FK_InvestigationID == note.FK_InvestigationID && a.FK_UserID == userId);
            var inStationScope = await _db.Dockets.AnyAsync(d =>
                d.FK_InvestigationID == note.FK_InvestigationID &&
                (isSystemAdmin || d.FK_StationID == requester!.FK_StationID));
            if ((!isAuthor && !isAssigned && !isSystemAdmin) || !inStationScope)
                return Forbid();

            var hasPending = await _db.InvestigationNoteArchiveRequests
                .AnyAsync(r => r.FK_NoteID == noteId && r.Status == "Pending");
            if (hasPending)
                return Conflict(new { success = false, message = "This note already has a pending archive request." });

            var request = new InvestigationNoteArchiveRequest
            {
                FK_NoteID = noteId,
                Reason = dto.Reason.Trim(),
                Status = "Pending",
                RequestedAt = DateTime.UtcNow,
                RequestedByUserID = userId
            };

            note.Status = "ArchivePending";
            await _db.InvestigationNoteArchiveRequests.AddAsync(request);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("ARCHIVE_NOTE_REQUEST", "InvestigationNote", noteId, null, new
            {
                request.ArchiveRequestID,
                request.FK_NoteID,
                request.Status,
                request.RequestedAt,
                request.RequestedByUserID
            }, userId, dto.Reason);

            return Ok(new { success = true, message = "Archive request submitted.", requestId = request.ArchiveRequestID });
        }

        [HttpGet("notes/archive-requests")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> GetArchiveRequests()
        {
            var userId = GetCurrentUserId();
            var requester = await _db.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.UserID == userId);
            if (requester == null) return Unauthorized();

            IQueryable<InvestigationNoteArchiveRequest> query = _db.InvestigationNoteArchiveRequests
                .Include(r => r.InvestigationNote)
                    .ThenInclude(n => n!.Author)
                .Where(r => r.Status == "Pending");

            if (requester.Role?.Name != "System Administrator")
            {
                var stationId = requester.FK_StationID;
                query = query.Where(request =>
                    request.InvestigationNote != null &&
                    _db.Dockets.Any(docket =>
                        docket.FK_InvestigationID == request.InvestigationNote.FK_InvestigationID &&
                        docket.FK_StationID == stationId));
            }

            var requests = await query
                .OrderBy(r => r.RequestedAt)
                .Select(r => new
                {
                    r.ArchiveRequestID,
                    NoteID = r.FK_NoteID,
                    r.Reason,
                    r.Status,
                    r.RequestedAt,
                    r.RequestedByUserID,
                    NoteDescription = r.InvestigationNote!.Description,
                    NoteStatus = r.InvestigationNote.Status,
                    AuthorName = (r.InvestigationNote.Author.FirstName + " " + r.InvestigationNote.Author.LastName).Trim()
                })
                .ToListAsync();

            return Ok(requests);
        }

        public class ReviewArchiveRequestDto
        {
            public string Decision { get; set; } = string.Empty;
            public string? RejectionReason { get; set; }
            public string? Notes { get; set; }
        }

        [HttpPatch("notes/archive-requests/{requestId}/review")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> ReviewArchiveRequest(int requestId, [FromBody] ReviewArchiveRequestDto dto)
        {
            var decision = dto.Decision?.Trim();
            if (!string.Equals(decision, "Approved", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(decision, "Rejected", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Decision must be Approved or Rejected." });

            var userId = GetCurrentUserId();
            var requester = await _db.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.UserID == userId);
            if (requester == null) return Unauthorized();

            IQueryable<InvestigationNoteArchiveRequest> requestQuery = _db.InvestigationNoteArchiveRequests
                .Include(r => r.InvestigationNote);
            if (requester.Role?.Name != "System Administrator")
            {
                var stationId = requester.FK_StationID;
                requestQuery = requestQuery.Where(archiveRequest =>
                    archiveRequest.InvestigationNote != null &&
                    _db.Dockets.Any(docket =>
                        docket.FK_InvestigationID == archiveRequest.InvestigationNote.FK_InvestigationID &&
                        docket.FK_StationID == stationId));
            }

            var request = await requestQuery.FirstOrDefaultAsync(r => r.ArchiveRequestID == requestId);
            if (request == null) return NotFound(new { success = false, message = "Archive request not found." });
            if (request.Status != "Pending")
                return BadRequest(new { success = false, message = "Only pending requests can be reviewed." });

            var approved = string.Equals(decision, "Approved", StringComparison.OrdinalIgnoreCase);
            request.Status = approved ? "Approved" : "Rejected";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByUserID = userId;
            request.ReviewNotes = approved ? dto.Notes : (dto.RejectionReason ?? dto.Notes);

            if (request.InvestigationNote != null)
                request.InvestigationNote.Status = approved ? "Archived" : "Active";

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync(approved ? "APPROVE_NOTE_ARCHIVE" : "REJECT_NOTE_ARCHIVE",
                "InvestigationNoteArchiveRequest", requestId, null, new
                {
                    request.ArchiveRequestID,
                    request.FK_NoteID,
                    request.Status,
                    request.ReviewedAt,
                    request.ReviewedByUserID,
                    request.ReviewNotes
                }, userId, request.ReviewNotes);

            return Ok(new { success = true, message = approved ? "Investigation note archived." : "Archive request rejected." });
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
