using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;

namespace SAPS.DDMS.API.Services
{
    public interface ISystemConfigService
    {
        Task<int> GetIntAsync(string key, int defaultValue);
        Task<bool> GetBoolAsync(string key, bool defaultValue);
        Task<string> GetStringAsync(string key, string defaultValue);
    }

    public class SystemConfigService : ISystemConfigService
    {
        private readonly ApplicationDbContext _db;

        public SystemConfigService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<int> GetIntAsync(string key, int defaultValue)
        {
            var value = await _db.SystemSettings
                .AsNoTracking()
                .Where(s => s.SettingKey == key)
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync();

            return int.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        public async Task<bool> GetBoolAsync(string key, bool defaultValue)
        {
            var value = await _db.SystemSettings
                .AsNoTracking()
                .Where(s => s.SettingKey == key)
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync();

            return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        public async Task<string> GetStringAsync(string key, string defaultValue)
        {
            var value = await _db.SystemSettings
                .AsNoTracking()
                .Where(s => s.SettingKey == key)
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }
    }
}
