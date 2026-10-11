using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models; 

namespace SAPS.DDMS.API.Repositories
{
    public class IncidentRepository: IIncidentRepository
    {
        private readonly ApplicationDbContext _context;

        public IncidentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Incident?> CreateIncidentAsync(Incident incident)
        {
            await _context.Incidents.AddAsync(incident);
            await _context.SaveChangesAsync();
            return incident;
        }

        public async Task<Incident?> GetIncidentByIdAsync(int incidentId)
        {
            return await _context.Incidents
                .Include(i => i.IncidentType)
                .Include(i => i.GeoLocation)
                .FirstOrDefaultAsync(i => i.IncidentID == incidentId);
        }

        public async Task<Incident?> GetIncidentWithDetailsAsync(int incidentId)
        {
            return await _context.Incidents
                .Include(i => i.IncidentType)
                .Include(i => i.GeoLocation)
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.IncidentID == incidentId);
        }

        public async Task UpdateIncidentAsync(Incident incident)
        {
            _context.Incidents.Update(incident);
            await _context.SaveChangesAsync();
        }

        public async Task<GeoLocation> CreateGeoLocationAsync(GeoLocation geoLocation)
        {
            await _context.GeoLocations.AddAsync(geoLocation);
            await _context.SaveChangesAsync();
            return geoLocation;
        }

        public async Task<List<IncidentType>> GetIncidentTypesAsync()
        {
            return await _context.IncidentTypes.ToListAsync();
        }

        public async Task<List<GeoLocation>> GetGeoLocationsAsync()
        {
            return await _context.GeoLocations.ToListAsync();
        }
    }
}
