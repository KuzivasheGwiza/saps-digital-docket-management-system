using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Repositories;


namespace SAPS.DDMS.API.Repositories
{
    public class InvestigationRepository : IInvestigationRepository
    {
        private readonly ApplicationDbContext _context;

        public InvestigationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<InvestigationNote?> GetNoteByIdAsync(int noteId)
        {
            return await _context.InvestigationNotes
                .Include(n => n.Author)
                .Include(n => n.Investigation)
                .FirstOrDefaultAsync(n => n.NoteID == noteId);
        }

        public async Task<List<InvestigationNote>> GetNotesByInvestigationIdAsync(int investigationId)
        {
            return await _context.InvestigationNotes
                .Include(n => n.Author)
                .Include(n => n.NoteAttachments)
                .Where(n => n.FK_InvestigationID == investigationId)
                .OrderBy(n => n.EntryDate)
                .ToListAsync();
        }

        public async Task<InvestigationNote> CreateNoteAsync(InvestigationNote note)
        {
            await _context.InvestigationNotes.AddAsync(note);
            await _context.SaveChangesAsync();
            return note;
        }

        public async Task UpdateNoteAsync(InvestigationNote note)
        {
            _context.InvestigationNotes.Update(note);
            await _context.SaveChangesAsync();
        }

        public async Task<NoteVersion> AddNoteVersionAsync(NoteVersion version)
        {
            await _context.NoteVersions.AddAsync(version);
            await _context.SaveChangesAsync();
            return version;
        }

        public async Task<List<NoteVersion>> GetNoteVersionsAsync(int noteId)
        {
            return await _context.NoteVersions
                .Include(nv => nv.User)
                .Where(nv => nv.FK_NoteID == noteId)
                .OrderByDescending(nv => nv.VersionNumber)
                .ToListAsync();
        }

        public async Task<NoteAttachment> AddAttachmentAsync(NoteAttachment attachment)
        {
            await _context.NoteAttachments.AddAsync(attachment);
            await _context.SaveChangesAsync();
            return attachment;
        }

        public async Task<NoteAttachment?> GetAttachmentByIdAsync(int attachmentId)
        {
            return await _context.NoteAttachments
                .FirstOrDefaultAsync(a => a.AttachmentID == attachmentId);
        }

        public async Task<bool> IsUserAssignedToInvestigationAsync(int userId, int investigationId)
        {
            return await _context.InvestigationAssignments
                .AnyAsync(ia => ia.FK_UserID == userId && ia.FK_InvestigationID == investigationId);
        }
    }
}