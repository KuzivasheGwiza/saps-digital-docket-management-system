using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Services;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/files")]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _files;

        public FilesController(ApplicationDbContext db, IFileStorageService files)
        {
            _db = db;
            _files = files;
        }

        [HttpGet("evidence/{evidenceId:int}")]
        [Authorize(Roles = "Investigating Officer,DSC Supervisor,Records Clerk")]
        public async Task<IActionResult> GetEvidenceImage(int evidenceId)
        {
            var evidence = await _db.Evidences
                .Include(item => item.Docket)
                    .ThenInclude(docket => docket.Investigation)
                        .ThenInclude(investigation => investigation.InvestigationAssignments)
                .FirstOrDefaultAsync(item => item.EvidenceID == evidenceId);
            if (evidence == null || string.IsNullOrWhiteSpace(evidence.ImagePath)) return NotFound();
            if (!await CanAccessDocket(evidence.Docket)) return Forbid();

            try
            {
                var bytes = await _files.GetFileAsync(evidence.ImagePath);
                return File(bytes, GetContentType(evidence.ImagePath));
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("note-attachments/{attachmentId:int}")]
        public async Task<IActionResult> DownloadNoteAttachment(int attachmentId)
        {
            var attachment = await _db.NoteAttachments
                .AsNoTracking()
                .Include(item => item.Note)
                    .ThenInclude(note => note!.Investigation)
                        .ThenInclude(investigation => investigation.Docket)
                .FirstOrDefaultAsync(item => item.AttachmentID == attachmentId);
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.FilePath)) return NotFound();
            if (!await CanAccessDocket(attachment.Note?.Investigation?.Docket)) return Forbid();

            try
            {
                var bytes = await _files.GetFileAsync(attachment.FilePath);
                return File(bytes, GetContentType(attachment.FileName), attachment.FileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        private async Task<bool> CanAccessDocket(SAPS.DDMS.API.Models.Docket? docket)
        {
            if (docket == null) return false;
            var userId = GetCurrentUserId();
            if (User.IsInRole("System Administrator")) return true;

            var stationId = await _db.Users
                .Where(user => user.UserID == userId)
                .Select(user => (int?)user.FK_StationID)
                .FirstOrDefaultAsync();
            if (stationId.HasValue && stationId.Value == docket.FK_StationID) return true;

            if (docket.Investigation?.InvestigationAssignments?.Any(assignment => assignment.FK_UserID == userId) == true)
                return true;

            return docket.FK_InvestigationID.HasValue && await _db.InvestigationAssignments
                .AsNoTracking()
                .AnyAsync(assignment =>
                    assignment.FK_InvestigationID == docket.FK_InvestigationID.Value &&
                    assignment.FK_UserID == userId);
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("userId")
                ?? User.FindFirstValue("sub");
            if (!int.TryParse(value, out var userId))
                throw new UnauthorizedAccessException("User id claim not found.");
            return userId;
        }

        private static string GetContentType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                _ => "application/octet-stream"
            };
        }
    }
}
