using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Repositories
{
    public interface IPersonRepository
    {
        Task<Person?> GetByIdAsync(int id);
        Task<Person?> GetByIdNumberAsync(string idNumber);
        Task<List<Person>> SearchAsync(string searchTerm, int stationId);
        Task<Person> CreateAsync(Person person);
        Task UpdateAsync(Person person);
        Task<bool> IdNumberExistsAsync(string idNumber);
        Task<DocketPersonLink> LinkPersonToDocketAsync(DocketPersonLink link);
        Task<List<DocketPersonLink>> GetDocketLinksForPersonAsync(int personId);
        Task<bool> IsPersonLinkedToDocketAsync(int personId, int docketId);
        Task<List<PersonRole>> GetPersonRolesAsync();
    }
}