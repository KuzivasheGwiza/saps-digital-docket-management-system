using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Repositories
{
    public interface IIncidentRepository
    {
        Task<Incident?> CreateIncidentAsync(Incident incident);
        Task<Incident?> GetIncidentByIdAsync(int incidentId);
        Task<Incident?> GetIncidentWithDetailsAsync(int incidentId);
        Task UpdateIncidentAsync(Incident incident);
        Task<GeoLocation> CreateGeoLocationAsync(GeoLocation geoLocation);
        Task<List<IncidentType>> GetIncidentTypesAsync();
        Task<List<GeoLocation>> GetGeoLocationsAsync();
    }
}
