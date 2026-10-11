using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Repositories;
using SAPS.DDMS.API.Services;


namespace SAPS.DDMS.API.Services
{
    public interface IIncidentService
    {
        Task<ServiceResult<IncidentResponseDto>> GetIncidentByIdAsync(int incidentId);
        Task<ServiceResult<List<IncidentType>>> GetIncidentTypesAsync();
        Task<ServiceResult<List<GeoLocation>>> GetGeoLocationsAsync();
    }

    public class IncidentService : IIncidentService
    {
        private readonly IIncidentRepository _incidentRepository;
        private readonly IAuditService _auditService;

        public IncidentService(IIncidentRepository incidentRepository, IAuditService auditService)
        {
            _incidentRepository = incidentRepository;
            _auditService = auditService;
        }

        public async Task<ServiceResult<IncidentResponseDto>> GetIncidentByIdAsync(int incidentId)
        {
            var incident = await _incidentRepository.GetIncidentWithDetailsAsync(incidentId);
            if (incident == null)
                return ServiceResult<IncidentResponseDto>.Failure("Incident not found.");

            var response = new IncidentResponseDto
            {
                IncidentID = incident.IncidentID,
                OccurrenceDate = incident.OccurrenceDate,
                LocationType = incident.LocationType,
                Description = incident.Description,
                IncidentType = incident.IncidentType?.TypeName ?? incident.CustomIncidentType,
                AreaName = incident.GeoLocation?.AreaName,
                Latitude = incident.GeoLocation?.Latitude,
                Longitude = incident.GeoLocation?.Longitude
            };

            return ServiceResult<IncidentResponseDto>.SuccessResult(response);
        }

        public async Task<ServiceResult<List<IncidentType>>> GetIncidentTypesAsync()
        {
            var types = await _incidentRepository.GetIncidentTypesAsync();
            return ServiceResult<List<IncidentType>>.SuccessResult(types);
        }

        public async Task<ServiceResult<List<GeoLocation>>> GetGeoLocationsAsync()
        {
            var locations = await _incidentRepository.GetGeoLocationsAsync();
            return ServiceResult<List<GeoLocation>>.SuccessResult(locations);
        }
    }
}
