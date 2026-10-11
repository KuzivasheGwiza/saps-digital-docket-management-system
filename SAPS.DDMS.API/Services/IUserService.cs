using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Helpers;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Repositories;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Security;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;


namespace SAPS.DDMS.API.Services
{
    public interface IUserService
    {
        Task<ServiceResult<List<UserResponseDto>>> GetAllUsersAsync(int? roleId = null, bool? isActive = null);
        Task<ServiceResult<UserResponseDto>> GetUserByIdAsync(int id);
        Task<ServiceResult<UserResponseDto>> CreateUserAsync(CreateUserDto createDto, int createdByUserId);
        Task<ServiceResult<UserResponseDto>> ResendActivationEmailAsync(int id, int requestedByUserId);
        Task<ServiceResult<bool>> UpdateUserAsync(int id, UpdateUserDto updateDto, int updatedByUserId);
        Task<List<RoleDto>> GetRolesAsync();
        Task<List<StationDto>> GetStationsAsync();
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuditService _auditService;
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _db;
        private readonly string _appUrl;

        public UserService(
            IUserRepository userRepository,
            IAuditService auditService,
            IEmailService emailService,
            ApplicationDbContext db,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _auditService = auditService;
            _emailService = emailService;
            _db = db;
            _appUrl = (configuration["AppUrl"] ?? "http://localhost:4200").TrimEnd('/');
        }

        public async Task<ServiceResult<List<UserResponseDto>>> GetAllUsersAsync(int? roleId = null, bool? isActive = null)
        {
            var users = await _userRepository.GetAllAsync(roleId, isActive);

            var response = users.Select(u => new UserResponseDto
            {
                UserID = u.UserID,
                FirstName = u.FirstName,
                LastName = u.LastName,
                PersonnelNumber = u.Username,
                Rank = u.Rank,
                EmailAddress = u.EmailAddress,
                PhoneNumber = u.PhoneNumber,
                RoleName = u.Role?.Name,
                StationName = u.Station?.StationName,
                AccountStatus = u.AccountStatus,
                NotificationPreferences = u.NotificationPreferences,
                IsActivated = u.IsActivated,
                IsActive = u.IsActive,
                IsLocked = u.IsLocked,
                DateCreated = u.DateCreated,
                LastLoginDate = u.LastLoginDate
            }).ToList();

            return ServiceResult<List<UserResponseDto>>.SuccessResult(response);
        }

        public async Task<ServiceResult<UserResponseDto>> GetUserByIdAsync(int id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return ServiceResult<UserResponseDto>.Failure("User not found");

            var response = new UserResponseDto
            {
                UserID = user.UserID,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PersonnelNumber = user.Username,
                Rank = user.Rank,
                EmailAddress = user.EmailAddress,
                PhoneNumber = user.PhoneNumber,
                RoleName = user.Role?.Name,
                StationName = user.Station?.StationName,
                AccountStatus = user.AccountStatus,
                NotificationPreferences = user.NotificationPreferences,
                IsActivated = user.IsActivated,
                IsActive = user.IsActive,
                IsLocked = user.IsLocked,
                DateCreated = user.DateCreated,
                LastLoginDate = user.LastLoginDate,
                Permissions = await PermissionCatalog.ResolveForRoleAsync(_db, user.FK_RoleID, user.Role?.Name)
            };

            return ServiceResult<UserResponseDto>.SuccessResult(response);
        }

        public async Task<ServiceResult<UserResponseDto>> CreateUserAsync(CreateUserDto createDto, int createdByUserId)
        {
            var personnelNumber = await GenerateUniquePersonnelNumberAsync();

            if (await _userRepository.EmailExistsAsync(createDto.EmailAddress))
                return ServiceResult<UserResponseDto>.Failure("Email address already exists.");

            var activationToken = PasswordHelper.GenerateActivationToken();
            var activationLink = $"{_appUrl}/activate?token={Uri.EscapeDataString(activationToken)}&userId=";

            var user = new User
            {
                FirstName = createDto.FirstName,
                LastName = createDto.LastName,
                Username = personnelNumber,
                PersonnelNumber = personnelNumber,
                Rank = createDto.Rank,
                EmailAddress = createDto.EmailAddress,
                PhoneNumber = createDto.PhoneNumber,
                PasswordHash = PasswordHelper.HashPassword(PasswordHelper.GenerateTemporaryPassword()),
                PasswordResetCode = PasswordHelper.HashToken(activationToken),
                PasswordResetExpiresAt = DateTime.UtcNow.AddHours(72),
                FK_RoleID = createDto.RoleID,
                FK_StationID = createDto.StationID,
                CreatedByUserID = createdByUserId,
                IsActivated = false,
                AccountStatus = "Pending Activation",
                DateCreated = DateTime.UtcNow
            };

            await _userRepository.CreateAsync(user);

            var role = (await _userRepository.GetRolesAsync()).FirstOrDefault(r => r.RoleID == user.FK_RoleID);
            var station = (await _userRepository.GetStationsAsync()).FirstOrDefault(s => s.StationID == user.FK_StationID);
            var userActivationLink = activationLink + user.UserID;
            var activationEmailSent = await _emailService.SendActivationEmailAsync(
                user.EmailAddress,
                $"{user.FirstName} {user.LastName}",
                userActivationLink);

            var response = new UserResponseDto
            {
                UserID = user.UserID,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PersonnelNumber = user.Username,
                Rank = user.Rank,
                EmailAddress = user.EmailAddress,
                PhoneNumber = user.PhoneNumber,
                RoleName = user.Role?.Name,
                StationName = user.Station?.StationName,
                AccountStatus = user.AccountStatus,
                NotificationPreferences = user.NotificationPreferences,
                IsActivated = user.IsActivated,
                IsActive = user.IsActive,
                IsLocked = user.IsLocked,
                DateCreated = user.DateCreated,
                ActivationEmailSent = activationEmailSent,
                ActivationLink = activationEmailSent ? null : userActivationLink
            };

            await _auditService.LogActionAsync("CREATE_USER", "User", user.UserID, null, response, createdByUserId);

            var message = activationEmailSent
                ? $"User account created successfully. Personnel number {personnelNumber} was generated and the activation email was sent."
                : $"User account created with personnel number {personnelNumber}, but the activation email could not be sent. Use the activation link returned in the response.";

            return ServiceResult<UserResponseDto>.SuccessResult(response, message);
        }

        private async Task<string> GenerateUniquePersonnelNumberAsync()
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = $"SAPS{DateTime.UtcNow:yy}{RandomNumberGenerator.GetInt32(0, 1_000_000):D6}";
                if (!await _userRepository.UsernameExistsAsync(candidate))
                    return candidate;
            }

            throw new InvalidOperationException("A unique personnel number could not be generated after multiple attempts.");
        }

        public async Task<ServiceResult<UserResponseDto>> ResendActivationEmailAsync(int id, int requestedByUserId)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return ServiceResult<UserResponseDto>.Failure("User not found");

            if (user.IsActivated)
                return ServiceResult<UserResponseDto>.Failure("User account is already activated.");

            var activationToken = PasswordHelper.GenerateActivationToken();
            var activationLink = $"{_appUrl}/activate?token={Uri.EscapeDataString(activationToken)}&userId={user.UserID}";

            user.PasswordResetCode = PasswordHelper.HashToken(activationToken);
            user.PasswordResetExpiresAt = DateTime.UtcNow.AddHours(72);
            await _userRepository.UpdateAsync(user);

            var activationEmailSent = await _emailService.SendActivationEmailAsync(
                user.EmailAddress,
                $"{user.FirstName} {user.LastName}",
                activationLink);

            var response = new UserResponseDto
            {
                UserID = user.UserID,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PersonnelNumber = user.Username,
                Rank = user.Rank,
                EmailAddress = user.EmailAddress,
                PhoneNumber = user.PhoneNumber,
                RoleName = user.Role?.Name,
                StationName = user.Station?.StationName,
                AccountStatus = user.AccountStatus,
                NotificationPreferences = user.NotificationPreferences,
                IsActivated = user.IsActivated,
                IsActive = user.IsActive,
                IsLocked = user.IsLocked,
                DateCreated = user.DateCreated,
                LastLoginDate = user.LastLoginDate,
                ActivationEmailSent = activationEmailSent,
                ActivationLink = activationEmailSent ? null : activationLink
            };

            var message = activationEmailSent
                ? "Activation email resent successfully."
                : "Activation email could not be sent. Use the activation link returned in the response.";

            await _auditService.LogActionAsync(
                "RESEND_ACTIVATION_LINK",
                "User",
                user.UserID,
                null,
                new
                {
                    ActivationEmailSent = activationEmailSent,
                    ActivationLinkExpiresAt = user.PasswordResetExpiresAt
                },
                requestedByUserId,
                message);

            return ServiceResult<UserResponseDto>.SuccessResult(response, message);
        }

        public async Task<ServiceResult<bool>> UpdateUserAsync(int id, UpdateUserDto updateDto, int updatedByUserId)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return ServiceResult<bool>.Failure("User not found");

            if (string.IsNullOrWhiteSpace(updateDto.Reason) || updateDto.Reason.Trim().Length < 3)
                return ServiceResult<bool>.Failure("A reason is required for account updates.");

            if (user.EmailAddress != updateDto.EmailAddress && await _userRepository.EmailExistsAsync(updateDto.EmailAddress))
                return ServiceResult<bool>.Failure("Email address already exists.");

            var oldValues = new { user.FirstName, user.LastName, user.Rank, user.EmailAddress, user.FK_StationID, user.FK_RoleID };

            user.FirstName = updateDto.FirstName;
            user.LastName = updateDto.LastName;
            user.Rank = updateDto.Rank;
            user.EmailAddress = updateDto.EmailAddress;
            user.PhoneNumber = updateDto.PhoneNumber;
            user.FK_StationID = updateDto.StationID;
            user.FK_RoleID = updateDto.RoleID;

            await _userRepository.UpdateAsync(user);

            var newValues = new { user.FirstName, user.LastName, user.Rank, user.EmailAddress, user.FK_StationID, user.FK_RoleID };

            await _auditService.LogActionAsync("UPDATE_USER", "User", user.UserID, oldValues, newValues,
                updatedByUserId, updateDto.Reason.Trim());

            return ServiceResult<bool>.SuccessResult(true, "User account updated successfully.");
        }

        public async Task<List<RoleDto>> GetRolesAsync()
        {
            var roles = await _userRepository.GetRolesAsync();
            return roles.Select(r => new RoleDto { Id = r.RoleID, Name = r.Name }).ToList();
        }

        public async Task<List<StationDto>> GetStationsAsync()
        {
            var stations = await _userRepository.GetStationsAsync();
            return stations.Select(s => new StationDto
            {
                Id = s.StationID,
                Name = s.StationName,
                Province = s.Province,
                Region = s.Region
            }).ToList();
        }
    }
}
