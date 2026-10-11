using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Repositories;

namespace SAPS.DDMS.API.Repositories
{
    public class PersonRepository : IPersonRepository
    {
        private readonly ApplicationDbContext _context;

        public PersonRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Person?> GetByIdAsync(int id)
        {
            return await _context.Persons
                .Include(p => p.CreatedByUser)
                .Include(p => p.PersonRole)
                .FirstOrDefaultAsync(p => p.PersonID == id);
        }

        public async Task<Person?> GetByIdNumberAsync(string idNumber)
        {
            return await _context.Persons
                .FirstOrDefaultAsync(p => p.IDNumber == idNumber);
        }

        public async Task<List<Person>> SearchAsync(string searchTerm, int stationId)
        {
            var normalized = searchTerm?.Trim() ?? string.Empty;
            return await _context.Persons
                .Include(p => p.PersonRole)
                .Where(p => (stationId == 0 ||
                             p.CreatedByUser.FK_StationID == stationId ||
                             p.DocketPersonLinks.Any(link => link.Docket.FK_StationID == stationId)) &&
                            (normalized == string.Empty ||
                             p.FirstName.Contains(normalized) ||
                             p.LastName.Contains(normalized) ||
                             p.IDNumber.Contains(normalized) ||
                             p.PhoneNumber.Contains(normalized) ||
                             p.EmailAddress.Contains(normalized)))
                .OrderBy(p => p.IsArchived)
                .ThenBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .Take(50)
                .ToListAsync();
        }

        public async Task<Person> CreateAsync(Person person)
        {
            await _context.Persons.AddAsync(person);
            await _context.SaveChangesAsync();
            return person;
        }

        public async Task UpdateAsync(Person person)
        {
            _context.Persons.Update(person);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> IdNumberExistsAsync(string idNumber)
        {
            if (string.IsNullOrEmpty(idNumber)) return false;
            return await _context.Persons.AnyAsync(p => p.IDNumber == idNumber);
        }

        public async Task<DocketPersonLink> LinkPersonToDocketAsync(DocketPersonLink link)
        {
            await _context.DocketPersonLinks.AddAsync(link);
            await _context.SaveChangesAsync();
            return link;
        }

        public async Task<List<DocketPersonLink>> GetDocketLinksForPersonAsync(int personId)
        {
            return await _context.DocketPersonLinks
                .Include(dpl => dpl.Docket)
                    .ThenInclude(d => d.DocketStatus)
                .Include(dpl => dpl.LinkedByUser)
                .Where(dpl => dpl.FK_PersonID == personId)
                .OrderByDescending(dpl => dpl.LinkedAt)
                .ToListAsync();
        }

        public async Task<bool> IsPersonLinkedToDocketAsync(int personId, int docketId)
        {
            return await _context.DocketPersonLinks
                .AnyAsync(dpl => dpl.FK_PersonID == personId && dpl.FK_DocketID == docketId);
        }

        public async Task<List<PersonRole>> GetPersonRolesAsync()
        {
            return await _context.PersonRoles
                .AsNoTracking()
                .Where(role => role.IsActive)
                .OrderBy(role => role.RoleName)
                .ToListAsync();
        }
    }
}
