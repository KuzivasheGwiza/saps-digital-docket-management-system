using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Repositories
{
    public interface IDocketRepository
    {
        Task<Docket?> GetByIdAsync(int id);
        Task<Docket?> GetByIdWithDetailsAsync(int id);
        Task<Docket?> GetByDocketNumberAsync(string docketNumber);
        Task<Docket?> GetByOBReferenceAsync(string obReference);
        Task<List<Docket>> GetAllWithDetailsAsync(string status = null);
        Task<List<Docket>> GetByStationIdAsync(int stationId, string status = null);
        Task<List<Docket>> GetByStationIdWithDetailsAsync(int stationId, string status = null);
        Task<List<Docket>> GetByInvestigatingOfficerAsync(int userId);
        Task<Docket> CreateAsync(Docket docket);
        Task<Investigation> CreateInvestigationAsync(Investigation investigation);
        Task UpdateAsync(Docket docket);
        Task<bool> OBReferenceExistsAsync(string obReference);
        Task<InvestigationAssignment> CreateAssignmentAsync(InvestigationAssignment assignment);
        Task<DocketTimer?> GetActiveTimerAsync(int docketId);
        Task<DocketTimer> AddTimerAsync(DocketTimer timer);
        Task UpdateTimerAsync(DocketTimer timer);
        Task<List<InvestigationAssignment>> GetAssignmentsForDocketAsync(int docketId);
        Task<List<User>> GetAvailableInvestigatingOfficersAsync(int stationId);
    }
    
}
