using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Repositories
{
    public interface IInvestigationRepository
    {
        Task<InvestigationNote?> GetNoteByIdAsync(int noteId);
        Task<List<InvestigationNote>> GetNotesByInvestigationIdAsync(int investigationId);
        Task<InvestigationNote> CreateNoteAsync(InvestigationNote note);
        Task UpdateNoteAsync(InvestigationNote note);
        Task<NoteVersion> AddNoteVersionAsync(NoteVersion version);
        Task<List<NoteVersion>> GetNoteVersionsAsync(int noteId);
        Task<NoteAttachment> AddAttachmentAsync(NoteAttachment attachment);
        Task<NoteAttachment?> GetAttachmentByIdAsync(int attachmentId);
        Task<bool> IsUserAssignedToInvestigationAsync(int userId, int investigationId);
    }
}
