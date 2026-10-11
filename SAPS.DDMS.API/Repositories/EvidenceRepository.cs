using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Repositories
{
    public class EvidenceRepository : IEvidenceRepository
    {
        private readonly ApplicationDbContext _context;

        public EvidenceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Evidence> CreateAsync(Evidence evidence)
        {
            await _context.Evidences.AddAsync(evidence);
            await _context.SaveChangesAsync();
            return evidence;
        }

        public async Task<Evidence> CreateWithInitialCustodyAsync(Evidence evidence, EvidenceTransfer initialCustody)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Evidences.AddAsync(evidence);
                await _context.SaveChangesAsync();

                initialCustody.FK_EvidenceID = evidence.EvidenceID;
                await _context.EvidenceTransfers.AddAsync(initialCustody);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return evidence;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Evidence?> GetByIdAsync(int id)
        {
            return await _context.Evidences
                .Include(e => e.EvidenceType)
                .Include(e => e.EvidenceLocation)
                .FirstOrDefaultAsync(e => e.EvidenceID == id);
        }

        public async Task<List<Evidence>> GetByDocketIdAsync(int docketId)
        {
            return await _context.Evidences
                .Include(e => e.EvidenceType)
                .Include(e => e.EvidenceLocation)
                .Where(e => e.FK_DocketID == docketId)
                .ToListAsync();
        }

        public async Task UpdateAsync(Evidence evidence)
        {
            _context.Evidences.Update(evidence);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetCountByDocketIdAsync(int docketId)
        {
            return await _context.Evidences.CountAsync(e => e.FK_DocketID == docketId);
        }

        public async Task<bool> IsUserAssignedToDocketAsync(int userId, int docketId)
        {
            var docket = await _context.Dockets
                .Include(d => d.Investigation)
                    .ThenInclude(i => i.InvestigationAssignments)
                .FirstOrDefaultAsync(d => d.DocketID == docketId);

            if (docket?.Investigation == null)
                return false;

            return docket.Investigation.InvestigationAssignments
                .Any(a => a.FK_UserID == userId);
        }

        public async Task<Docket?> GetDocketAsync(int docketId)
        {
            return await _context.Dockets
                .Include(d => d.DocketStatus)
                .Include(d => d.Investigation)
                .FirstOrDefaultAsync(d => d.DocketID == docketId);
        }

        public async Task<List<EvidenceType>> GetEvidenceTypesAsync()
        {
            return await _context.EvidenceTypes.ToListAsync();
        }

        public async Task<List<EvidenceLocation>> GetEvidenceLocationsAsync()
        {
            return await _context.EvidenceLocations.ToListAsync();
        }
    }
}
