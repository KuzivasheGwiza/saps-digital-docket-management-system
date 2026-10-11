using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;
using System.Security.Claims;


namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;
        private readonly IWebHostEnvironment _environment;

        public UserController(IUserService userService, ApplicationDbContext db, IAuditService auditService, IWebHostEnvironment environment)
        {
            _userService = userService;
            _db = db;
            _auditService = auditService;
            _environment = environment;
        }

        /// <summary>
        /// Returns all user accounts to system administrators.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = PermissionCatalog.UsersManage)]
        public async Task<IActionResult> GetAllUsers([FromQuery] int? roleId, [FromQuery] bool? isActive)
        {
            var result = await _userService.GetAllUsersAsync(roleId, isActive);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            await _auditService.LogActionAsync("READ_USER_LIST", "User", null, null,
                new { roleId, isActive, Count = result.Data?.Count ?? 0 }, GetCurrentUserId());

            return Ok(result.Data);
        }

        /// <summary>
        /// Returns a user account to system administrators.
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Policy = PermissionCatalog.UsersManage)]
        public async Task<IActionResult> GetUserById(int id)
        {
            var result = await _userService.GetUserByIdAsync(id);

            if (!result.Success)
                return NotFound(new { success = false, message = result.Message });

            await _auditService.LogActionAsync("READ_USER", "User", id, null,
                new { ViewedUserID = id }, GetCurrentUserId());

            return Ok(result.Data);
        }

        /// <summary>
        /// Creates a user account and activation workflow.
        /// </summary>
        [HttpPost]
        [Authorize(Policy = PermissionCatalog.UsersManage)]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto createUserDto)
        {
            var currentUserId = GetCurrentUserId();
            var result = await _userService.CreateUserAsync(createUserDto, currentUserId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message, user = result.Data });
        }

        [HttpPost("{id}/resend-activation")]
        [Authorize(Policy = PermissionCatalog.UsersManage)]
        public async Task<IActionResult> ResendActivationEmail(int id)
        {
            var result = await _userService.ResendActivationEmailAsync(id, GetCurrentUserId());

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message, user = result.Data });
        }

        /// <summary>
        /// Updates a user account and records role changes.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Policy = PermissionCatalog.UsersManage)]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto updateUserDto)
        {
            var currentUserId = GetCurrentUserId();
            var before = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == id);
            if (before == null)
                return NotFound(new { success = false, message = "User not found" });

            var oldRoleId = before.FK_RoleID;
            var oldRoleName = before.Role?.Name;
            var result = await _userService.UpdateUserAsync(id, updateUserDto, currentUserId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            if (oldRoleId != updateUserDto.RoleID)
            {
                var newRole = await _db.Roles.FindAsync(updateUserDto.RoleID);
                await _db.UserRoleHistories.AddAsync(new UserRoleHistory
                {
                    FK_UserID = id,
                    OldRoleID = oldRoleId,
                    NewRoleID = updateUserDto.RoleID,
                    OldRoleName = oldRoleName,
                    NewRoleName = newRole?.Name,
                    Reason = updateUserDto.Reason ?? "Role changed during user update",
                    ChangedAt = DateTime.UtcNow,
                    ChangedByUserID = currentUserId
                });
                await _db.SaveChangesAsync();
                await _auditService.LogActionAsync("CHANGE_USER_ROLE", "User", id,
                    new { RoleID = oldRoleId, RoleName = oldRoleName },
                    new { RoleID = updateUserDto.RoleID, RoleName = newRole?.Name },
                    currentUserId,
                    updateUserDto.Reason);
            }

            return Ok(new { success = true, message = result.Message });
        }

        public class UserActionDto
        {
            public string? Reason { get; set; }
        }

        [HttpPatch("{id}/deactivate")]
        [Authorize(Policy = PermissionCatalog.UsersManage)]
        public async Task<IActionResult> DeactivateUser(int id, [FromBody] UserActionDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null) return NotFound(new { success = false, message = "User not found" });

            if (user.Role?.Name == "System Administrator")
            {
                var activeAdmins = await _db.Users.Include(u => u.Role)
                    .CountAsync(u => u.UserID != id && u.Role!.Name == "System Administrator" && u.IsActive && u.AccountStatus != "Deactivated");
                if (activeAdmins == 0)
                    return BadRequest(new { success = false, message = "Cannot deactivate the only active System Administrator." });
            }

            var old = new { user.IsActive, user.AccountStatus };
            user.IsActive = false;
            user.IsLocked = true;
            user.AccountStatus = "Deactivated";
            await _db.SaveChangesAsync();
            var deactivatedAt = DateTime.UtcNow;
            await _db.Sessions
                .Where(session => session.FK_UserID == id && session.Logout == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.Logout, deactivatedAt));
            await _auditService.LogActionAsync("DEACTIVATE_USER", "User", id, old, new { user.IsActive, user.AccountStatus }, currentUserId, dto?.Reason);

            return Ok(new { success = true, message = "User account deactivated." });
        }

        [HttpPatch("{id}/suspend")]
        [Authorize(Policy = PermissionCatalog.UsersManage)]
        public async Task<IActionResult> SuspendUser(int id, [FromBody] UserActionDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound(new { success = false, message = "User not found" });
            if (!user.IsActive || user.AccountStatus == "Deactivated")
                return BadRequest(new { success = false, message = "Only active accounts can be suspended." });

            var old = new { user.IsActive, user.AccountStatus };
            user.IsActive = false;
            user.AccountStatus = "Suspended";
            await _db.SaveChangesAsync();
            var suspendedAt = DateTime.UtcNow;
            await _db.Sessions
                .Where(session => session.FK_UserID == id && session.Logout == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.Logout, suspendedAt));
            await _auditService.LogActionAsync("SUSPEND_USER", "User", id, old, new { user.IsActive, user.AccountStatus }, currentUserId, dto?.Reason);

            return Ok(new { success = true, message = "User account suspended." });
        }

        [HttpPatch("{id}/reactivate")]
        [Authorize(Policy = PermissionCatalog.UsersManage)]
        public async Task<IActionResult> ReactivateUser(int id, [FromBody] UserActionDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound(new { success = false, message = "User not found" });
            if (string.Equals(user.AccountStatus, "Deactivated", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Permanently deactivated accounts cannot be reactivated." });

            var old = new { user.IsActive, user.AccountStatus };
            user.IsActive = true;
            user.AccountStatus = user.IsActivated ? "Active" : "Pending Activation";
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("REACTIVATE_USER", "User", id, old, new { user.IsActive, user.AccountStatus }, currentUserId, dto?.Reason);

            return Ok(new { success = true, message = "User account reactivated." });
        }

        [HttpPatch("{id}/unlock")]
        [Authorize(Policy = PermissionCatalog.UsersManage)]
        public async Task<IActionResult> UnlockUser(int id, [FromBody] UserActionDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound(new { success = false, message = "User not found" });

            var old = new { user.IsLocked, user.FailedLoginAttempts, user.PasswordResetCode };
            user.IsLocked = false;
            user.FailedLoginAttempts = 0;
            user.PasswordResetCode = null;
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("UNLOCK_USER", "User", id, old, new { user.IsLocked, user.FailedLoginAttempts }, currentUserId, dto?.Reason);

            return Ok(new { success = true, message = "User account unlocked." });
        }

        [HttpGet("{id}/role-history")]
        [Authorize(Policy = PermissionCatalog.UsersManage)]
        public async Task<IActionResult> GetRoleHistory(int id)
        {
            var history = await _db.UserRoleHistories
                .Where(h => h.FK_UserID == id)
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new
                {
                    h.UserRoleHistoryID,
                    h.OldRoleID,
                    h.NewRoleID,
                    h.OldRoleName,
                    h.NewRoleName,
                    h.Reason,
                    h.ChangedAt,
                    h.ChangedByUserID
                })
                .ToListAsync();

            return Ok(history);
        }

        /// <summary>
        /// Returns roles available for account administration.
        /// </summary>
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _userService.GetRolesAsync();
            return Ok(roles);
        }

        /// <summary>
        /// Returns stations available for account administration.
        /// </summary>
        [HttpGet("stations")]
        public async Task<IActionResult> GetStations()
        {
            var stations = await _db.Stations
                .OrderBy(s => s.Province)
                .ThenBy(s => s.StationName)
                .Select(s => new
                {
                    Id = s.StationID,
                    Name = s.StationName,
                    s.Code,
                    Province = s.Province ?? "Unknown",
                    Region = s.Region ?? "",
                    DocketCount = s.Dockets.Count
                })
                .ToListAsync();
            return Ok(stations);
        }

        public class StationMutationDto
        {
            public string Name { get; set; } = string.Empty;
            public string Province { get; set; } = string.Empty;
            public string Region { get; set; } = string.Empty;
            public string? Code { get; set; }
        }

        public class CityMutationDto
        {
            public string Name { get; set; } = string.Empty;
            public string Province { get; set; } = string.Empty;
        }

        public class CityUpdateDto : CityMutationDto
        {
            public string OriginalName { get; set; } = string.Empty;
            public string OriginalProvince { get; set; } = string.Empty;
        }

        /// <summary>
        /// Gets cities configured by administrators together with cities already
        /// represented by existing stations. This lets a city be prepared before
        /// its first station is created.
        /// </summary>
        [HttpGet("cities")]
        public async Task<IActionResult> GetCities()
        {
            const string cityKeyPrefix = "HIERARCHY_CITY:";
            var stationCities = await _db.Stations
                .Where(station => !string.IsNullOrWhiteSpace(station.Province) && !string.IsNullOrWhiteSpace(station.Region))
                .Select(station => new { Province = station.Province!.Trim(), Name = station.Region!.Trim() })
                .ToListAsync();
            var configuredCities = await _db.SystemSettings
                .Where(setting => setting.SettingKey.StartsWith(cityKeyPrefix))
                .Select(setting => new { Id = setting.SettingID, Province = setting.SettingValue.Trim(), Name = setting.Description.Trim() })
                .ToListAsync();

            var cities = stationCities
                .Select(city => new { Id = 0, city.Province, city.Name })
                .Concat(configuredCities)
                .Where(city => !string.IsNullOrWhiteSpace(city.Province) && !string.IsNullOrWhiteSpace(city.Name))
                .GroupBy(city => new { Province = city.Province.ToUpper(), Name = city.Name.ToUpper() })
                .Select(group => group.OrderByDescending(city => city.Id).First())
                .OrderBy(city => city.Province)
                .ThenBy(city => city.Name)
                .ToList();

            return Ok(cities);
        }

        [HttpPost("cities")]
        [Authorize(Roles = "System Administrator")]
        public async Task<IActionResult> CreateCity([FromBody] CityMutationDto dto)
        {
            var province = dto.Province?.Trim() ?? string.Empty;
            var cityName = dto.Name?.Trim() ?? string.Empty;
            if (province.Length is < 2 or > 50 || cityName.Length is < 2 or > 50)
                return BadRequest(new { success = false, message = "A city name and province of 2 to 50 characters are required." });

            var exists = await _db.Stations.AnyAsync(station => station.Province == province && station.Region == cityName) ||
                         await _db.SystemSettings.AnyAsync(setting => setting.SettingKey.StartsWith("HIERARCHY_CITY:") && setting.SettingValue == province && setting.Description == cityName);
            if (exists)
                return BadRequest(new { success = false, message = "This city is already available in the selected province." });

            var city = new SystemSetting
            {
                SettingKey = $"HIERARCHY_CITY:{Guid.NewGuid():N}",
                SettingValue = province,
                Description = cityName,
                DataType = "text",
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserID = GetCurrentUserId()
            };
            await _db.SystemSettings.AddAsync(city);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("CREATE_CITY", "City", city.SettingID, null, new { Province = province, Name = cityName }, GetCurrentUserId());

            return Ok(new { success = true, id = city.SettingID, province, name = cityName, message = $"City '{cityName}' added." });
        }

        [HttpPut("cities")]
        [Authorize(Roles = "System Administrator")]
        public async Task<IActionResult> UpdateCity([FromBody] CityUpdateDto dto)
        {
            var originalProvince = dto.OriginalProvince?.Trim() ?? string.Empty;
            var originalName = dto.OriginalName?.Trim() ?? string.Empty;
            var province = dto.Province?.Trim() ?? string.Empty;
            var cityName = dto.Name?.Trim() ?? string.Empty;
            if (new[] { originalProvince, originalName, province, cityName }.Any(value => value.Length is < 2 or > 50))
                return BadRequest(new { success = false, message = "Original and updated city names and provinces must contain 2 to 50 characters." });

            var stations = await _db.Stations
                .Where(station => station.Province == originalProvince && station.Region == originalName)
                .ToListAsync();
            var settings = await _db.SystemSettings
                .Where(setting => setting.SettingKey.StartsWith("HIERARCHY_CITY:") &&
                                  setting.SettingValue == originalProvince && setting.Description == originalName)
                .ToListAsync();
            if (stations.Count == 0 && settings.Count == 0)
                return NotFound(new { success = false, message = "City not found." });

            if (await _db.Stations.AnyAsync(station => station.Province == province && station.Region == cityName &&
                    !(station.Province == originalProvince && station.Region == originalName)))
                return BadRequest(new { success = false, message = "The updated city already exists in that province." });

            foreach (var station in stations) { station.Province = province; station.Region = cityName; }
            foreach (var setting in settings) { setting.SettingValue = province; setting.Description = cityName; setting.UpdatedAt = DateTime.UtcNow; setting.UpdatedByUserID = GetCurrentUserId(); }
            if (settings.Count == 0)
                await _db.SystemSettings.AddAsync(new SystemSetting
                {
                    SettingKey = $"HIERARCHY_CITY:{Guid.NewGuid():N}", SettingValue = province,
                    Description = cityName, DataType = "text", UpdatedAt = DateTime.UtcNow,
                    UpdatedByUserID = GetCurrentUserId()
                });

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("UPDATE_CITY", "City", null,
                new { Province = originalProvince, Name = originalName }, new { Province = province, Name = cityName }, GetCurrentUserId());
            return Ok(new { success = true, message = "City and linked stations updated." });
        }

        [HttpDelete("cities")]
        [Authorize(Roles = "System Administrator")]
        public async Task<IActionResult> DeleteCity([FromQuery] string province, [FromQuery] string name)
        {
            province = province?.Trim() ?? string.Empty;
            name = name?.Trim() ?? string.Empty;
            if (await _db.Stations.AnyAsync(station => station.Province == province && station.Region == name))
                return BadRequest(new { success = false, message = "City cannot be removed while stations are linked to it." });

            var settings = await _db.SystemSettings
                .Where(setting => setting.SettingKey.StartsWith("HIERARCHY_CITY:") && setting.SettingValue == province && setting.Description == name)
                .ToListAsync();
            if (settings.Count == 0) return NotFound(new { success = false, message = "City not found." });
            _db.SystemSettings.RemoveRange(settings);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("DELETE_CITY", "City", null, new { Province = province, Name = name }, null, GetCurrentUserId());
            return Ok(new { success = true, message = "City removed." });
        }

        [HttpPost("stations")]
        [Authorize(Roles = "System Administrator")]
        public async Task<IActionResult> CreateStation([FromBody] StationMutationDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Province) || string.IsNullOrWhiteSpace(dto.Region))
                return BadRequest(new { success = false, message = "Station name, province and region are required." });

            var exists = await _db.Stations.AnyAsync(s => s.StationName == dto.Name.Trim() && s.Province == dto.Province.Trim());
            if (exists)
                return BadRequest(new { success = false, message = "A station with this name already exists in the selected province." });

            var station = new Station
            {
                StationName = dto.Name.Trim(),
                Province = dto.Province.Trim(),
                Region = dto.Region.Trim(),
                Code = dto.Code?.Trim()
            };

            await _db.Stations.AddAsync(station);
            await _db.SaveChangesAsync();

            var currentUserId = GetCurrentUserId();
            await _auditService.LogActionAsync("CREATE_STATION", "Station", station.StationID, null, station, currentUserId);

            return Ok(new
            {
                Id = station.StationID,
                Name = station.StationName,
                station.Code,
                Province = station.Province ?? "Unknown",
                Region = station.Region ?? "",
                DocketCount = 0
            });
        }

        [HttpPut("stations/{id:int}")]
        [Authorize(Roles = "System Administrator")]
        public async Task<IActionResult> UpdateStation(int id, [FromBody] StationMutationDto dto)
        {
            var station = await _db.Stations.FindAsync(id);
            if (station == null)
                return NotFound(new { success = false, message = "Station not found." });

            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Province) || string.IsNullOrWhiteSpace(dto.Region))
                return BadRequest(new { success = false, message = "Station name, province and region are required." });

            var old = new { station.StationName, station.Province, station.Region, station.Code };
            station.StationName = dto.Name.Trim();
            station.Province = dto.Province.Trim();
            station.Region = dto.Region.Trim();
            station.Code = dto.Code?.Trim();

            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("UPDATE_STATION", "Station", id, old, new { station.StationName, station.Province, station.Region, station.Code }, GetCurrentUserId());

            return Ok(new { success = true, message = "Station updated." });
        }

        [HttpDelete("stations/{id:int}")]
        [Authorize(Roles = "System Administrator")]
        public async Task<IActionResult> DeleteStation(int id)
        {
            var station = await _db.Stations
                .Include(s => s.Users)
                .Include(s => s.Dockets)
                .FirstOrDefaultAsync(s => s.StationID == id);

            if (station == null)
                return NotFound(new { success = false, message = "Station not found." });

            if ((station.Users?.Any() == true) || (station.Dockets?.Any() == true))
                return BadRequest(new { success = false, message = "Station cannot be deleted while users or dockets are linked to it." });

            var old = new { station.StationID, station.StationName, station.Province, station.Region };
            _db.Stations.Remove(station);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("DELETE_STATION", "Station", id, old, null, GetCurrentUserId());

            return Ok(new { success = true, message = "Station deleted." });
        }

        /// <summary>
        /// Returns the current user's profile.
        /// </summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetCurrentUserId();
            var result = await _userService.GetUserByIdAsync(userId);

            if (!result.Success)
                return NotFound(new { success = false, message = result.Message });

            return Ok(result.Data);
        }

        [HttpGet("profile/photo")]
        public IActionResult GetProfilePhoto()
        {
            var directory = GetProfilePhotoDirectory();
            var match = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, $"{GetCurrentUserId()}.*").FirstOrDefault()
                : null;
            if (match == null)
                return NotFound(new { success = false, message = "No profile photo has been uploaded." });

            var contentType = Path.GetExtension(match).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
            return PhysicalFile(match, contentType, enableRangeProcessing: true);
        }

        [HttpPost("profile/photo")]
        [RequestSizeLimit(2 * 1024 * 1024)]
        public async Task<IActionResult> UploadProfilePhoto(IFormFile photo)
        {
            if (photo == null || photo.Length == 0)
                return BadRequest(new { success = false, message = "Select a profile photo before uploading." });
            if (photo.Length > 2 * 1024 * 1024)
                return BadRequest(new { success = false, message = "The profile photo cannot exceed 2 MB." });

            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            var allowed = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["image/jpeg"] = new[] { ".jpg", ".jpeg" },
                ["image/png"] = new[] { ".png" },
                ["image/webp"] = new[] { ".webp" }
            };
            if (!allowed.TryGetValue(photo.ContentType, out var extensions) || !extensions.Contains(extension))
                return BadRequest(new { success = false, message = "Only JPEG, PNG or WebP profile photos are allowed." });

            await using var inspectionStream = photo.OpenReadStream();
            var signature = new byte[12];
            var bytesRead = await inspectionStream.ReadAsync(signature.AsMemory(0, signature.Length));
            var validSignature = bytesRead >= 3 && (
                (photo.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) && signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF) ||
                (photo.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) && bytesRead >= 8 && signature.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) ||
                (photo.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) && bytesRead >= 12 && System.Text.Encoding.ASCII.GetString(signature, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(signature, 8, 4) == "WEBP"));
            if (!validSignature)
                return BadRequest(new { success = false, message = "The selected file content does not match a supported image format." });

            var userId = GetCurrentUserId();
            var directory = GetProfilePhotoDirectory();
            Directory.CreateDirectory(directory);
            foreach (var existing in Directory.EnumerateFiles(directory, $"{userId}.*"))
                System.IO.File.Delete(existing);
            var path = Path.Combine(directory, $"{userId}{extension}");
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await photo.CopyToAsync(output);

            await _auditService.LogActionAsync("UPDATE_PROFILE_PHOTO", "User", userId, null, new { ContentType = photo.ContentType, photo.Length }, userId);
            return Ok(new { success = true, message = "Profile photo updated successfully." });
        }

        private string GetProfilePhotoDirectory() => Path.Combine(
            _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "uploads", "profiles");

        public class UpdateProfileDto
        {
            public string? PhoneNumber { get; set; }
            public string? NotificationPreferences { get; set; }
        }

        [HttpPatch("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { success = false, message = "User not found" });

            var old = new { user.PhoneNumber, user.NotificationPreferences };
            user.PhoneNumber = dto.PhoneNumber;
            user.NotificationPreferences = dto.NotificationPreferences;
            user.CommunicationPreference = dto.NotificationPreferences ?? user.CommunicationPreference;
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("UPDATE_PROFILE", "User", userId, old,
                new { user.PhoneNumber, user.NotificationPreferences }, userId);

            return Ok(new { success = true, message = "Profile updated successfully." });
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
