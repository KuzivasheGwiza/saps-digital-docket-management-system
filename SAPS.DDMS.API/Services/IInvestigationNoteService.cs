// Services/IInvestigationNoteService.cs
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Repositories;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Data;
using Microsoft.EntityFrameworkCore;

namespace SAPS.DDMS.API.Services
{
    public interface IInvestigationNoteService
    {
        Task<ServiceResult<InvestigationNote>> CreateNoteAsync(int investigationId, string description, int authorUserId, IFormFile? attachment);
        Task<ServiceResult<List<InvestigationNote>>> GetNotesByInvestigationAsync(int investigationId, int requestingUserId);
        Task<ServiceResult<InvestigationNote>> UpdateNoteAsync(int noteId, string newDescription, string reason, int requestingUserId);
        Task<ServiceResult<List<NoteVersion>>> GetNoteVersionsAsync(int noteId, int requestingUserId);
    }

    public class InvestigationNoteService : IInvestigationNoteService
    {
        private readonly IInvestigationRepository _investigationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAuditService _auditService;
        private readonly IFileStorageService _fileStorageService;
        private readonly ApplicationDbContext _db;

        public InvestigationNoteService(
            IInvestigationRepository investigationRepository,
            IUserRepository userRepository,
            IAuditService auditService,
            IFileStorageService fileStorageService,
            ApplicationDbContext db)
        {
            _investigationRepository = investigationRepository;
            _userRepository = userRepository;
            _auditService = auditService;
            _fileStorageService = fileStorageService;
            _db = db;
        }

        // UC 4.1 - Create Investigation Note
        public async Task<ServiceResult<InvestigationNote>> CreateNoteAsync(int investigationId, string description, int authorUserId, IFormFile? attachment)
        {
            var author = await _userRepository.GetByIdAsync(authorUserId);
            if (author == null)
                return ServiceResult<InvestigationNote>.Failure("User not found.");
            if (!await IsInStationScopeAsync(investigationId, author))
                return ServiceResult<InvestigationNote>.Failure("You do not have permission to add notes to this investigation.");

            var isSystemAdmin = author.Role?.Name == "System Administrator";
            if (!isSystemAdmin && !await _investigationRepository.IsUserAssignedToInvestigationAsync(authorUserId, investigationId))
                return ServiceResult<InvestigationNote>.Failure("Only assigned Investigating Officers may add investigation notes.");

            var normalizedDescription = description?.Trim() ?? string.Empty;
            if (normalizedDescription.Length < 10 || normalizedDescription.Length > 256)
                return ServiceResult<InvestigationNote>.Failure("Note must contain between 10 and 256 characters.");

            var docketStatus = await _db.Dockets
                .Where(d => d.FK_InvestigationID == investigationId)
                .Select(d => d.DocketStatus.StatusName)
                .FirstOrDefaultAsync();
            if (docketStatus == null)
                return ServiceResult<InvestigationNote>.Failure("Investigation docket not found.");
            if (docketStatus is "CLOSED" or "ARCHIVED")
                return ServiceResult<InvestigationNote>.Failure("Notes cannot be added to a closed or archived docket.");

            if (attachment != null && attachment.Length > 0)
            {
                if (attachment.Length > 10 * 1024 * 1024)
                    return ServiceResult<InvestigationNote>.Failure("File exceeds 10 MB limit.");

                var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/png", "image/jpg" };
                if (!allowedTypes.Contains(attachment.ContentType))
                    return ServiceResult<InvestigationNote>.Failure("File type not supported. Only PDF, JPG, PNG are allowed.");
            }

            var note = new InvestigationNote
            {
                Description = normalizedDescription,
                FK_InvestigationID = investigationId,
                FK_AuthorUserID = authorUserId,
                EntryDate = DateTime.UtcNow
            };

            await _investigationRepository.CreateNoteAsync(note);

            // Handle file upload (Complexity #11)
            if (attachment != null && attachment.Length > 0)
            {
                var filePath = await _fileStorageService.SaveFileAsync(attachment, "notes");
                var fileHash = _fileStorageService.ComputeFileHash(attachment);

                var noteAttachment = new NoteAttachment
                {
                    FileName = attachment.FileName,
                    FilePath = filePath,
                    FileSize = attachment.Length,
                    Hash = fileHash,
                    FileType = Path.GetExtension(attachment.FileName),
                    FK_NoteID = note.NoteID,
                    FK_UserID = authorUserId,
                    UploadedAt = DateTime.UtcNow
                };

                await _investigationRepository.AddAttachmentAsync(noteAttachment);
            }

            await _auditService.LogActionAsync("CREATE_NOTE", "InvestigationNote", note.NoteID, null,
                new
                {
                    note.NoteID,
                    note.Description,
                    note.FK_InvestigationID,
                    note.FK_AuthorUserID
                },
                authorUserId);

            return ServiceResult<InvestigationNote>.SuccessResult(note, "Investigation note saved successfully.");
        }

        // UC 4.2 - Read Investigation Notes
        public async Task<ServiceResult<List<InvestigationNote>>> GetNotesByInvestigationAsync(int investigationId, int requestingUserId)
        {
            var user = await _userRepository.GetByIdAsync(requestingUserId);
            if (user == null)
                return ServiceResult<List<InvestigationNote>>.Failure("User not found.");
            if (!await IsInStationScopeAsync(investigationId, user))
                return ServiceResult<List<InvestigationNote>>.Failure("You do not have permission to view these notes.");

            var isAssigned = await _investigationRepository.IsUserAssignedToInvestigationAsync(requestingUserId, investigationId);
            var isSupervisor = user?.Role?.Name == "DSC Supervisor" ||
                               user?.Role?.Name == "Docket Administrator" ||
                               user?.Role?.Name == "System Administrator";

            if (!isAssigned && !isSupervisor)
                return ServiceResult<List<InvestigationNote>>.Failure("You do not have permission to view these notes.");

            var notes = await _investigationRepository.GetNotesByInvestigationIdAsync(investigationId);

            await _auditService.LogActionAsync("READ_NOTES", "Investigation", investigationId, null, null, requestingUserId);

            return ServiceResult<List<InvestigationNote>>.SuccessResult(notes);
        }

        // UC 4.3 - Update Investigation Note (with version history)
        public async Task<ServiceResult<InvestigationNote>> UpdateNoteAsync(int noteId, string newDescription, string reason, int requestingUserId)
        {
            var requestingUser = await _userRepository.GetByIdAsync(requestingUserId);
            if (requestingUser == null)
                return ServiceResult<InvestigationNote>.Failure("User not found.");

            var note = await _investigationRepository.GetNoteByIdAsync(noteId);
            if (note == null)
                return ServiceResult<InvestigationNote>.Failure("Note not found.");
            if (!await IsInStationScopeAsync(note.FK_InvestigationID, requestingUser))
                return ServiceResult<InvestigationNote>.Failure("You do not have permission to edit this note.");

            var isAuthor = note.FK_AuthorUserID == requestingUserId;
            var isDocketAdmin = requestingUser.Role?.Name == "Docket Administrator";
            var isSystemAdmin = requestingUser.Role?.Name == "System Administrator";

            if (!isAuthor && !isDocketAdmin && !isSystemAdmin)
                return ServiceResult<InvestigationNote>.Failure("Only the original author or Docket Administrator may edit this note.");

            if (note.Status is "ArchivePending" or "Archived")
                return ServiceResult<InvestigationNote>.Failure("Archived notes and notes pending archive review are read-only.");

            var docketStatus = await _db.Dockets
                .Where(d => d.FK_InvestigationID == note.FK_InvestigationID)
                .Select(d => d.DocketStatus.StatusName)
                .FirstOrDefaultAsync();
            if (docketStatus is "CLOSED" or "ARCHIVED")
                return ServiceResult<InvestigationNote>.Failure("Notes on a closed or archived docket are read-only.");

            var normalizedDescription = newDescription?.Trim() ?? string.Empty;
            if (normalizedDescription.Length < 10 || normalizedDescription.Length > 256)
                return ServiceResult<InvestigationNote>.Failure("Note must contain between 10 and 256 characters.");

            if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
                return ServiceResult<InvestigationNote>.Failure("A reason of at least 10 characters is required for note edits.");

            // Get existing versions to calculate next version number
            var existingVersions = await _investigationRepository.GetNoteVersionsAsync(noteId);
            var nextVersion = existingVersions.Count + 1;

            // Save version history (immutable - preserves original text)
            var version = new NoteVersion
            {
                VersionNumber = nextVersion,
                NoteTextBefore = note.Description,
                NoteTextAfter = normalizedDescription,
                Reason = reason,
                FK_NoteID = noteId,
                FK_UserID = requestingUserId,
                EditedAt = DateTime.UtcNow
            };

            await using var transaction = await _db.Database.BeginTransactionAsync();
            await _investigationRepository.AddNoteVersionAsync(version);

            // Update note
            note.Description = normalizedDescription;
            await _investigationRepository.UpdateNoteAsync(note);

            await _auditService.LogActionAsync("UPDATE_NOTE", "InvestigationNote", noteId,
                new { Description = version.NoteTextBefore }, new { Description = version.NoteTextAfter },
                requestingUserId, reason);

            await transaction.CommitAsync();

            return ServiceResult<InvestigationNote>.SuccessResult(note, "Note updated successfully. Version history preserved.");
        }

        public async Task<ServiceResult<List<NoteVersion>>> GetNoteVersionsAsync(int noteId, int requestingUserId)
        {
            var note = await _investigationRepository.GetNoteByIdAsync(noteId);
            if (note == null)
                return ServiceResult<List<NoteVersion>>.Failure("Note not found.");

            var isAssigned = await _investigationRepository.IsUserAssignedToInvestigationAsync(requestingUserId, note.FK_InvestigationID);
            var user = await _userRepository.GetByIdAsync(requestingUserId);
            if (user == null)
                return ServiceResult<List<NoteVersion>>.Failure("User not found.");
            if (!await IsInStationScopeAsync(note.FK_InvestigationID, user))
                return ServiceResult<List<NoteVersion>>.Failure("You do not have permission to view note versions.");

            var isAuthorized = isAssigned ||
                               user?.Role?.Name == "DSC Supervisor" ||
                               user?.Role?.Name == "Docket Administrator" ||
                               user?.Role?.Name == "System Administrator";

            if (!isAuthorized)
                return ServiceResult<List<NoteVersion>>.Failure("You do not have permission to view note versions.");

            var versions = await _investigationRepository.GetNoteVersionsAsync(noteId);

            return ServiceResult<List<NoteVersion>>.SuccessResult(versions);
        }

        private async Task<bool> IsInStationScopeAsync(int investigationId, User user)
        {
            if (user.Role?.Name == "System Administrator")
                return true;

            return await _db.Dockets.AnyAsync(docket =>
                docket.FK_InvestigationID == investigationId &&
                docket.FK_StationID == user.FK_StationID);
        }
    }
}
