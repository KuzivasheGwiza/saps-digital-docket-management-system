using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;
using System.Security.Claims;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/system-config")]
    [Authorize(Policy = PermissionCatalog.ConfigurationManage)]
    public class SystemConfigController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;

        public SystemConfigController(ApplicationDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _db.SystemSettings
                .OrderBy(s => s.SettingID)
                .Select(s => new
                {
                    s.SettingID,
                    s.SettingKey,
                    s.SettingValue,
                    s.Description,
                    s.DataType,
                    s.UpdatedAt,
                    UpdatedBy = s.UpdatedByUserID == null
                        ? "System"
                        : _db.Users
                            .Where(u => u.UserID == s.UpdatedByUserID)
                            .Select(u => u.FirstName + " " + u.LastName)
                            .FirstOrDefault() ?? "System Administrator"
                })
                .ToListAsync();

            return Ok(settings);
        }

        public class UpdateSettingDto
        {
            public string Value { get; set; } = string.Empty;
        }

        [HttpPatch("{key}")]
        public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Value))
                return BadRequest(new { success = false, message = "A setting value is required." });

            var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null)
                return NotFound(new { success = false, message = "Setting not found." });

            if (setting.DataType == "int" && (!int.TryParse(dto.Value, out var numericValue) || numericValue < 1))
                return BadRequest(new { success = false, message = "This setting must be a positive whole number." });

            if (setting.DataType == "bool" && !bool.TryParse(dto.Value, out _))
                return BadRequest(new { success = false, message = "This setting must be true or false." });

            var userId = GetCurrentUserId();
            var oldValue = setting.SettingValue;
            setting.SettingValue = dto.Value.Trim();
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedByUserID = userId;

            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("UPDATE_SYSTEM_SETTING", "SystemSetting", setting.SettingID, oldValue, setting.SettingValue, userId, key);

            return Ok(new { success = true, message = "Setting updated." });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : 0;
        }
    }
}
