using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Repositories;


namespace SAPS.DDMS.API.Repositories
{
    public class DocketRepository : IDocketRepository
    {
        private readonly ApplicationDbContext _context;

        public DocketRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        private IQueryable<Docket> WithDocketDetails()
        {
            return _context.Dockets
                // Several collection navigations are required for docket cards.
                // Split queries avoid a cartesian join that can time out as data grows.
                .AsSplitQuery()
                .Include(d => d.DocketStatus)
                .Include(d => d.Station)
                .Include(d => d.CreatedByUser)
                .Include(d => d.Incident)
                    .ThenInclude(i => i.IncidentType)
                .Include(d => d.Incident)
                    .ThenInclude(i => i.GeoLocation)
                .Include(d => d.Investigation)
                    .ThenInclude(i => i.InvestigationAssignments)
                        .ThenInclude(a => a.User)
                .Include(d => d.DocketPersonLinks)
                    .ThenInclude(l => l.Person)
                        .ThenInclude(p => p.PersonRole)
                .Include(d => d.DocketPrivacyFlags);
        }

        public async Task<Docket?> GetByIdAsync(int id)
        {
            return await WithDocketDetails()
                .FirstOrDefaultAsync(d => d.DocketID == id);
        }

        public async Task<Docket?> GetByIdWithDetailsAsync(int id)
        {
            return await WithDocketDetails()
                .FirstOrDefaultAsync(d => d.DocketID == id);
        }

        public async Task<Docket?> GetByDocketNumberAsync(string docketNumber)
        {
            return await _context.Dockets
                .FirstOrDefaultAsync(d => d.DocketNumber == docketNumber);
        }

        public async Task<Docket?> GetByOBReferenceAsync(string obReference)
        {
            return await _context.Dockets
                .FirstOrDefaultAsync(d => d.OBReferenceNumber == obReference);
        }

        public async Task<List<Docket>> GetByStationIdAsync(int stationId, string status = null)
        {
            var query = WithDocketDetails()
                .AsNoTracking()
                .Where(d => d.FK_StationID == stationId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(d => d.DocketStatus.StatusName == status);

            return await query.OrderByDescending(d => d.DateCreated).ToListAsync();
        }

        public async Task<List<Docket>> GetAllWithDetailsAsync(string status = null)
        {
            var query = WithDocketDetails().AsNoTracking();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(d => d.DocketStatus.StatusName == status);

            return await query.OrderByDescending(d => d.DateCreated).ToListAsync();
        }

        public async Task<List<Docket>> GetByStationIdWithDetailsAsync(int stationId, string status = null)
        {
            var query = WithDocketDetails()
                .AsNoTracking()
                .Where(d => d.FK_StationID == stationId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(d => d.DocketStatus.StatusName == status);

            return await query.OrderByDescending(d => d.DateCreated).ToListAsync();
        }

        public async Task<List<Docket>> GetByInvestigatingOfficerAsync(int userId)
        {
            // First get investigations assigned to this officer
            var assignments = await _context.InvestigationAssignments
                .AsNoTracking()
                .Where(ia => ia.FK_UserID == userId)
                .Select(ia => ia.FK_InvestigationID)
                .ToListAsync();

            return await _context.Dockets
                .AsNoTracking()
                .AsSplitQuery()
                .Include(d => d.DocketStatus)
                .Include(d => d.Station)
                .Include(d => d.CreatedByUser)
                .Include(d => d.Incident)
                    .ThenInclude(i => i.IncidentType)
                .Include(d => d.Incident)
                    .ThenInclude(i => i.GeoLocation)
                .Include(d => d.Investigation)
                    .ThenInclude(i => i.InvestigationAssignments)
                        .ThenInclude(a => a.User)
                .Include(d => d.DocketPersonLinks)
                    .ThenInclude(l => l.Person)
                        .ThenInclude(p => p.PersonRole)
                .Include(d => d.DocketPrivacyFlags)
                .Where(d => d.FK_InvestigationID != null && assignments.Contains(d.FK_InvestigationID.Value))
                .OrderByDescending(d => d.DateAllocated)
                .ToListAsync();
        }

        public async Task<Docket> CreateAsync(Docket docket)
        {
            await _context.Dockets.AddAsync(docket);
            await _context.SaveChangesAsync();
            return docket;
        }

        public async Task<Investigation> CreateInvestigationAsync(Investigation investigation)
        {
            await _context.Investigations.AddAsync(investigation);
            await _context.SaveChangesAsync();
            return investigation;
        }

        public async Task UpdateAsync(Docket docket)
        {
            var trackedEntry = _context.ChangeTracker.Entries<Docket>()
                .FirstOrDefault(entry => entry.Entity.DocketID == docket.DocketID);

            if (trackedEntry != null)
            {
                trackedEntry.CurrentValues.SetValues(docket);
            }
            else
            {
                _context.Dockets.Attach(docket);
                _context.Entry(docket).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> OBReferenceExistsAsync(string obReference)
        {
            return await _context.Dockets.AnyAsync(d => d.OBReferenceNumber == obReference);
        }

        public async Task<InvestigationAssignment> CreateAssignmentAsync(InvestigationAssignment assignment)
        {
            await _context.InvestigationAssignments.AddAsync(assignment);
            await _context.SaveChangesAsync();
            return assignment;
        }

        public async Task<DocketTimer?> GetActiveTimerAsync(int docketId)
        {
            return await _context.DocketTimers
                .FirstOrDefaultAsync(t => t.FK_DocketID == docketId && !t.IsCompleted);
        }

        public async Task<DocketTimer> AddTimerAsync(DocketTimer timer)
        {
            await _context.DocketTimers.AddAsync(timer);
            await _context.SaveChangesAsync();
            return timer;
        }

        public async Task UpdateTimerAsync(DocketTimer timer)
        {
            _context.DocketTimers.Update(timer);
            await _context.SaveChangesAsync();
        }

        public async Task<List<InvestigationAssignment>> GetAssignmentsForDocketAsync(int docketId)
        {
            var docket = await GetByIdAsync(docketId);
            if (docket?.FK_InvestigationID == null)
                return new List<InvestigationAssignment>();

            return await _context.InvestigationAssignments
                .Include(ia => ia.User)
                .Where(ia => ia.FK_InvestigationID == docket.FK_InvestigationID)
                .ToListAsync();
        }

        public async Task<List<User>> GetAvailableInvestigatingOfficersAsync(int stationId)
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Station)
                .Where(u => u.FK_StationID == stationId &&
                       u.IsActive &&
                       u.IsActivated &&
                       !u.IsLocked &&
                       u.AccountStatus == "Active" &&
                       u.Role.Name == "Investigating Officer")
                .ToListAsync();
        }
    }
}
