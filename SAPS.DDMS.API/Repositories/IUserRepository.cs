using SAPS.DDMS.API.Models;

namespace SAPS.DDMS.API.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByResetTokenAsync(string tokenHash);
        Task<User?> GetByActivationTokenAsync(string tokenHash);
        Task<List<User>> GetAllAsync(int? roleId = null, bool? isActive = null);
        Task<User> CreateAsync(User user);
        Task UpdateAsync(User user);
        Task<bool> UsernameExistsAsync(string username);
        Task<bool> EmailExistsAsync(string email);
        Task<List<Role>> GetRolesAsync();
        Task<List<Station>> GetStationsAsync();
        Task<List<User>> GetUsersByRoleAndStationAsync(string roleName, int stationId);
        Task<Session> CreateSessionAsync(Session session);
        Task<Session?> GetActiveSessionAsync(int userId);
        Task UpdateSessionAsync(Session session);
    }
}
