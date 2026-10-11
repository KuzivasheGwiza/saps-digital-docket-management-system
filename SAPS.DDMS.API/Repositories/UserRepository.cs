using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Data;
namespace SAPS.DDMS.API.Repositories
{
    public class UserRepository: IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            var normalized = username.Trim().ToUpperInvariant();
            return await _context.Users
                .Include(u => u.Role)
                    .ThenInclude(role => role!.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission)
                .Include(u => u.Station)
                .FirstOrDefaultAsync(u => u.Username == normalized);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return await _context.Users
                .Include(u => u.Role)
                    .ThenInclude(role => role!.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission)
                .Include(u => u.Station)
                .FirstOrDefaultAsync(u => u.EmailAddress == normalized);
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Role)
                    .ThenInclude(role => role!.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission)
                .Include(u => u.Station)
                .FirstOrDefaultAsync(u => u.UserID == id);
        }

        public async Task<User?> GetByResetTokenAsync(string tokenHash)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.PasswordResetCode == tokenHash);
        }

        public async Task<User?> GetByActivationTokenAsync(string tokenHash)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.PasswordResetCode == tokenHash && !u.IsActivated);
        }

        public async Task<List<User>> GetAllAsync(int? roleId = null, bool? isActive = null)
        {
            var query = _context.Users
                .Include(u => u.Role)
                .Include(u => u.Station)
                .AsQueryable();

            if (roleId.HasValue)
                query = query.Where(u => u.FK_RoleID == roleId.Value);

            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            return await query.ToListAsync();
        }

        public async Task<User> CreateAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username == username);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Users.AnyAsync(u => u.EmailAddress == email);
        }

        public async Task<List<Role>> GetRolesAsync()
        {
            return await _context.Roles.ToListAsync();
        }

        public async Task<List<Station>> GetStationsAsync()
        {
            return await _context.Stations.ToListAsync();
        }

        public async Task<List<User>> GetUsersByRoleAndStationAsync(string roleName, int stationId)
        {
            return await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.Name == roleName && u.FK_StationID == stationId && u.IsActive && u.IsActivated && !u.IsLocked)
                .ToListAsync();
        }

        public async Task<Session> CreateSessionAsync(Session session)
        {
            await _context.Sessions.AddAsync(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<Session?> GetActiveSessionAsync(int userId)
        {
            return await _context.Sessions
                .FirstOrDefaultAsync(s => s.FK_UserID == userId && s.Logout == null);
        }

        public async Task UpdateSessionAsync(Session session)
        {
            _context.Sessions.Update(session);
            await _context.SaveChangesAsync();
        }
    }
}
