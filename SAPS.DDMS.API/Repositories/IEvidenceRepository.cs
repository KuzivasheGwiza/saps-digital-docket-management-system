using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Repositories
{
    public interface IEvidenceRepository
    {
        Task<Evidence> CreateAsync(Evidence evidence);
        Task<Evidence> CreateWithInitialCustodyAsync(Evidence evidence, EvidenceTransfer initialCustody);
        Task<Evidence?> GetByIdAsync(int id);
        Task<List<Evidence>> GetByDocketIdAsync(int docketId);
        Task UpdateAsync(Evidence evidence);
        Task<int> GetCountByDocketIdAsync(int docketId);
     
        Task<bool> IsUserAssignedToDocketAsync(int userId, int docketId);
        Task<Docket?> GetDocketAsync(int docketId);
        Task<List<EvidenceType>> GetEvidenceTypesAsync();
        Task<List<EvidenceLocation>> GetEvidenceLocationsAsync();
    }


}
