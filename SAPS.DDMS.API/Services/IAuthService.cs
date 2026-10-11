using SAPS.DDMS.API.Models;
// Services/IAuthService.cs
using SAPS.DDMS.API.Helpers;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Repositories;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Data;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using SAPS.DDMS.API.Security;


namespace SAPS.DDMS.API.Services
{
    public interface IAuthService
    {
        Task<ServiceResult<LoginResponseDto>> LoginAsync(LoginDto loginDto, string ipAddress);
        Task<ServiceResult<bool>> ActivateAccountAsync(ActivateAccountDto activateDto);
        Task<ServiceResult<bool>> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto);
        Task<ServiceResult<bool>> ForgotPasswordAsync(string email);
        Task<ServiceResult<bool>> ResetPasswordAsync(string token, string newPassword);
        Task<ServiceResult<LoginResponseDto>> VerifyOtpAsync(VerifyOtpDto verifyOtpDto, string ipAddress);
        Task<ServiceResult<bool>> LogoutAsync(int userId);
    }

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly JwtHelper _jwtHelper;
        private readonly IAuditService _auditService;
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _db;
        private readonly ISmsService _smsService;
        private readonly ISystemConfigService _config;
        private readonly string _appUrl;
        private readonly int _absoluteTokenMinutes;

        public AuthService(
            IUserRepository userRepository,
            JwtHelper jwtHelper,
            IAuditService auditService,
            IEmailService emailService,
            ApplicationDbContext db,
            ISmsService smsService,
            ISystemConfigService config,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _jwtHelper = jwtHelper;
            _auditService = auditService;
            _emailService = emailService;
            _db = db;
            _smsService = smsService;
            _config = config;
            _appUrl = (configuration["AppUrl"] ?? "http://localhost:4200").TrimEnd('/');
            _absoluteTokenMinutes = Math.Max(
                configuration.GetValue<int?>("JwtSettings:ExpiryMinutes") ?? 480,
                120);
        }

        // UC 2.1 - Log In
        public async Task<ServiceResult<LoginResponseDto>> LoginAsync(LoginDto loginDto, string ipAddress)
        {
            var user = await FindLoginUserAsync(loginDto.PersonnelNumber);
            if (user == null)
                return ServiceResult<LoginResponseDto>.Failure("Invalid personnel number, email address or password.");

            bool passwordValid = PasswordHelper.VerifyPassword(loginDto.Password, user.PasswordHash);

            if (!user.IsActivated)
                return ServiceResult<LoginResponseDto>.Failure("Account not activated. Please check your email.");

            if (user.IsLocked)
                return ServiceResult<LoginResponseDto>.Failure("Account is locked. Contact system administrator.");

            if (!user.IsActive)
                return ServiceResult<LoginResponseDto>.Failure("Account is deactivated.");

            if (string.Equals(user.AccountStatus, "Suspended", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<LoginResponseDto>.Failure("Account is suspended. Contact system administrator.");

            if (string.Equals(user.AccountStatus, "Deactivated", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<LoginResponseDto>.Failure("Account is deactivated.");

            if (!passwordValid)
            {
                var maxAttempts = await _config.GetIntAsync("MAX_LOGIN_ATTEMPTS", 3);
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= maxAttempts)
                    user.IsLocked = true;
                await _userRepository.UpdateAsync(user);

                await _auditService.LogActionAsync(
                    user.IsLocked ? "ACCOUNT_LOCKED" : "LOGIN_FAILED",
                    "User",
                    user.UserID,
                    null,
                    new { user.FailedLoginAttempts, MaxAttempts = maxAttempts },
                    user.UserID,
                    user.IsLocked ? "Maximum failed login attempts reached" : "Invalid password",
                    ipAddress);

                return ServiceResult<LoginResponseDto>.Failure("Invalid personnel number, email address or password.");
            }

            var twoFactorRequired = await _config.GetBoolAsync("TWO_FACTOR_REQUIRED", false);
            if (twoFactorRequired || user.IsTwoFactorEnabled)
                return await StartOtpChallengeAsync(user, ipAddress);

            return await CompleteLoginAsync(user, ipAddress);
        }

        public async Task<ServiceResult<LoginResponseDto>> VerifyOtpAsync(VerifyOtpDto verifyOtpDto, string ipAddress)
        {
            var user = await FindLoginUserAsync(verifyOtpDto.PersonnelNumber);
            if (user == null)
                return ServiceResult<LoginResponseDto>.Failure("Invalid or expired one-time PIN");

            if (!user.IsActivated || !user.IsActive || user.IsLocked ||
                string.Equals(user.AccountStatus, "Suspended", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.AccountStatus, "Deactivated", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<LoginResponseDto>.Failure("Account is not eligible for login.");

            if (string.IsNullOrWhiteSpace(user.OtpCodeHash) ||
                !user.OtpExpiresAt.HasValue ||
                user.OtpExpiresAt.Value < DateTime.UtcNow ||
                user.OtpCodeHash != PasswordHelper.HashToken(verifyOtpDto.OtpCode.Trim()))
            {
                await _auditService.LogActionAsync("LOGIN_OTP_FAILED", "User", user.UserID, null, null, user.UserID, ipAddress: ipAddress);
                return ServiceResult<LoginResponseDto>.Failure("Invalid or expired one-time PIN");
            }

            return await CompleteLoginAsync(user, ipAddress);
        }

        private Task<User?> FindLoginUserAsync(string? identifier)
        {
            var normalized = identifier?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
                return Task.FromResult<User?>(null);

            return normalized.Contains('@')
                ? _userRepository.GetByEmailAsync(normalized.ToLowerInvariant())
                : _userRepository.GetByUsernameAsync(normalized.ToUpperInvariant());
        }

        private async Task<ServiceResult<LoginResponseDto>> StartOtpChallengeAsync(User user, string ipAddress)
        {
            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var expiryMinutes = await _config.GetIntAsync("OTP_EXPIRY_MINUTES", 5);
            user.OtpCodeHash = PasswordHelper.HashToken(otp);
            user.OtpRequestedAt = DateTime.UtcNow;
            user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
            await _userRepository.UpdateAsync(user);

            await _smsService.SendOtpAsync(user, otp);
            if (!string.IsNullOrWhiteSpace(user.EmailAddress))
            {
                await _emailService.SendNotificationEmailAsync(
                    user.EmailAddress,
                    "SAPS DDMS Login One-Time PIN",
                    $"Your SAPS DDMS one-time PIN is <strong>{otp}</strong>. It expires in {expiryMinutes} minutes.");
            }

            await _auditService.LogActionAsync("LOGIN_OTP_SENT", "User", user.UserID, null, new { user.OtpExpiresAt }, user.UserID, ipAddress: ipAddress);

            var deliveryHint = string.IsNullOrWhiteSpace(user.PhoneNumber)
                ? "One-time PIN generated. No phone number is saved, so the PIN was also sent to the registered email address if email is configured."
                : $"One-time PIN sent by SMS to phone ending {user.PhoneNumber[^Math.Min(4, user.PhoneNumber.Length)..]}.";

            return ServiceResult<LoginResponseDto>.SuccessResult(new LoginResponseDto
            {
                RequiresOtp = true,
                PendingPersonnelNumber = user.Username,
                OtpExpiresAt = user.OtpExpiresAt,
                DeliveryHint = deliveryHint,
                User = null
            }, "Enter the one-time PIN to complete login.");
        }

        private async Task<ServiceResult<LoginResponseDto>> CompleteLoginAsync(User user, string ipAddress)
        {
            user.FailedLoginAttempts = 0;
            user.LastLoginDate = DateTime.UtcNow;
            user.OtpCodeHash = null;
            user.OtpExpiresAt = null;
            user.OtpRequestedAt = null;
            await _userRepository.UpdateAsync(user);

            var sessionTimeoutMinutes = await _config.GetIntAsync("SESSION_TIMEOUT_MINUTES", 60);
            var token = _jwtHelper.GenerateJwtToken(user, user.Role?.Name ?? "User", _absoluteTokenMinutes);
            var tokenExpiresAt = DateTime.UtcNow.AddMinutes(_absoluteTokenMinutes);
            var idleExpiresAt = DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes);

            var session = new Session
            {
                FK_UserID = user.UserID,
                Login = DateTime.UtcNow,
                ExpiryTime = idleExpiresAt,
                LastActivity = DateTime.UtcNow,
                TokenHash = PasswordHelper.HashToken(token)
            };
            await _userRepository.CreateSessionAsync(session);

            await _db.SessionLogs.AddAsync(new SessionLog
            {
                FK_SessionID = session.SessionID,
                ActionType = "LOGIN",
                AffectedEntity = "Session",
                AffectedRecordID = session.SessionID,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync("LOGIN", "User", user.UserID, null, null, user.UserID, ipAddress: ipAddress);

            var response = new LoginResponseDto
            {
                Token = token,
                ExpiresAt = tokenExpiresAt,
                SessionTimeoutMinutes = sessionTimeoutMinutes,
                User = new UserInfoDto
                {
                    Id = user.UserID,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PersonnelNumber = user.Username,
                    Rank = user.Rank,
                    EmailAddress = user.EmailAddress,
                    PhoneNumber = user.PhoneNumber,
                    Role = user.Role?.Name ?? "User",
                    StationName = user.Station?.StationName,
                    IsActivated = user.IsActivated,
                    IsActive = user.IsActive,
                    IsLocked = user.IsLocked,
                    DateCreated = user.DateCreated,
                    LastLoginDate = user.LastLoginDate
                }
            };

            response.User.Permissions = await PermissionCatalog.ResolveForRoleAsync(
                _db, user.FK_RoleID, user.Role?.Name);

            return ServiceResult<LoginResponseDto>.SuccessResult(response, "Login successful");
        }

        // UC 2.2 - Activate Account / Create Password (First time login)
        public async Task<ServiceResult<bool>> ActivateAccountAsync(ActivateAccountDto activateDto)
        {
            if (!PasswordHelper.MeetsComplexityRequirements(activateDto.Password))
                return ServiceResult<bool>.Failure("Password must be at least 12 characters with uppercase, lowercase, number, and special character.");

            var user = await _userRepository.GetByIdAsync(activateDto.UserId);
            if (user == null)
                return ServiceResult<bool>.Failure("User not found");

            if (user.PasswordResetCode != PasswordHelper.HashToken(activateDto.Token))
                return ServiceResult<bool>.Failure("Invalid activation token");

            if (!user.PasswordResetExpiresAt.HasValue || user.PasswordResetExpiresAt.Value < DateTime.UtcNow)
                return ServiceResult<bool>.Failure("Activation link has expired. Ask an administrator to resend it.");

            if (user.IsActivated)
                return ServiceResult<bool>.Failure("Account already activated");

            if (await IsPasswordReusedAsync(user, activateDto.Password))
                return ServiceResult<bool>.Failure("You may not reuse your temporary or a recent password. Please choose a new password.");

            RecordCurrentPassword(user, "Activation");
            user.PasswordHash = PasswordHelper.HashPassword(activateDto.Password);
            user.PasswordResetCode = null;
            user.PasswordResetExpiresAt = null;
            user.IsActivated = true;
            user.IsActive = true;
            user.AccountStatus = "Active";

            await _userRepository.UpdateAsync(user);

            await _auditService.LogActionAsync("ACTIVATE_ACCOUNT", "User", user.UserID, "inactive", "active", user.UserID);
            await _auditService.LogActionAsync("CREATE_PASSWORD", "User", user.UserID, "temporary", "permanent", user.UserID);

            return ServiceResult<bool>.SuccessResult(true, "Account activated successfully");
        }

        // UC 2.3 - Change Password
        public async Task<ServiceResult<bool>> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
                return ServiceResult<bool>.Failure("User not found");

            if (!PasswordHelper.VerifyPassword(changePasswordDto.CurrentPassword, user.PasswordHash))
                return ServiceResult<bool>.Failure("Current password is incorrect");

            if (changePasswordDto.NewPassword != changePasswordDto.ConfirmNewPassword)
                return ServiceResult<bool>.Failure("New passwords do not match");

            if (!PasswordHelper.MeetsComplexityRequirements(changePasswordDto.NewPassword))
                return ServiceResult<bool>.Failure("New password does not meet complexity requirements");

            if (await IsPasswordReusedAsync(user, changePasswordDto.NewPassword))
                return ServiceResult<bool>.Failure("New password must be different from your current and recent passwords");

            RecordCurrentPassword(user, "Change");
            user.PasswordHash = PasswordHelper.HashPassword(changePasswordDto.NewPassword);
            await _userRepository.UpdateAsync(user);

            await _auditService.LogActionAsync("CHANGE_PASSWORD", "User", userId, null, null, userId);

            await _emailService.SendNotificationEmailAsync(user.EmailAddress, "Password Changed",
                "Your password has been successfully changed. If you did not make this change, please contact support immediately.");

            return ServiceResult<bool>.SuccessResult(true, "Password updated successfully");
        }

        public async Task<ServiceResult<bool>> ForgotPasswordAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
                return ServiceResult<bool>.SuccessResult(true, "If an active account matches that email address, a one-time PIN will be sent.");

            if (!user.IsActive || string.Equals(user.AccountStatus, "Suspended", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.AccountStatus, "Deactivated", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<bool>.SuccessResult(true, "If an active account matches that email address, a one-time PIN will be sent.");

            var resetToken = PasswordHelper.GenerateSixDigitOtp();
            var expiryMinutes = await _config.GetIntAsync("OTP_EXPIRY_MINUTES", 5);

            user.PasswordResetCode = PasswordHelper.HashToken(resetToken);
            user.PasswordResetExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
            await _userRepository.UpdateAsync(user);

            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            var resetEmailSent = await _emailService.SendPasswordResetOtpEmailAsync(
                user.EmailAddress,
                string.IsNullOrWhiteSpace(fullName) ? "SAPS DDMS user" : fullName,
                resetToken,
                expiryMinutes);
            await _auditService.LogActionAsync(
                resetEmailSent ? "ISSUE_PASSWORD_RESET_OTP" : "PASSWORD_RESET_EMAIL_FAILED",
                "User",
                user.UserID,
                null,
                new
                {
                    ResetEmailSent = resetEmailSent,
                    PasswordResetExpiresAt = user.PasswordResetExpiresAt
                },
                user.UserID,
                "Self-service password reset requested.");

            return ServiceResult<bool>.SuccessResult(true, "If an active account matches that email address, a one-time PIN will be sent.");
        }

        public async Task<ServiceResult<bool>> ResetPasswordAsync(string token, string newPassword)
        {
            var tokenHash = PasswordHelper.HashToken(token);
            var user = await _userRepository.GetByResetTokenAsync(tokenHash);

            if (user == null)
                return ServiceResult<bool>.Failure("Invalid or expired reset token");

            if (user.PasswordResetExpiresAt.HasValue && user.PasswordResetExpiresAt.Value < DateTime.UtcNow)
            {
                user.PasswordResetCode = null;
                user.PasswordResetExpiresAt = null;
                await _userRepository.UpdateAsync(user);
                return ServiceResult<bool>.Failure("Invalid or expired reset token");
            }

            if (!PasswordHelper.MeetsComplexityRequirements(newPassword))
                return ServiceResult<bool>.Failure("Password does not meet complexity requirements");

            if (await IsPasswordReusedAsync(user, newPassword))
                return ServiceResult<bool>.Failure("New password must be different from your current and recent passwords");

            RecordCurrentPassword(user, "Reset");
            user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            user.PasswordResetCode = null;
            user.PasswordResetExpiresAt = null;
            await _userRepository.UpdateAsync(user);

            await _auditService.LogActionAsync("RESET_PASSWORD", "User", user.UserID, null, null, user.UserID);

            return ServiceResult<bool>.SuccessResult(true, "Password reset successfully");
        }

        private async Task<bool> IsPasswordReusedAsync(User user, string candidatePassword)
        {
            if (!string.IsNullOrWhiteSpace(user.PasswordHash) &&
                PasswordHelper.VerifyPassword(candidatePassword, user.PasswordHash))
                return true;

            var historyLimit = Math.Clamp(await _config.GetIntAsync("PASSWORD_HISTORY_COUNT", 5), 1, 24);
            var recentHashes = await _db.UserPasswordHistories
                .Where(item => item.FK_UserID == user.UserID)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => item.PasswordHash)
                .Take(historyLimit)
                .ToListAsync();

            return recentHashes.Any(hash => PasswordHelper.VerifyPassword(candidatePassword, hash));
        }

        private void RecordCurrentPassword(User user, string changeType)
        {
            if (string.IsNullOrWhiteSpace(user.PasswordHash)) return;
            _db.UserPasswordHistories.Add(new UserPasswordHistory
            {
                FK_UserID = user.UserID,
                PasswordHash = user.PasswordHash,
                ChangeType = changeType,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task<ServiceResult<bool>> LogoutAsync(int userId)
        {
            var activeSession = await _userRepository.GetActiveSessionAsync(userId);
            if (activeSession != null)
            {
                activeSession.Logout = DateTime.UtcNow;
                await _userRepository.UpdateSessionAsync(activeSession);

                await _db.SessionLogs.AddAsync(new SessionLog
                {
                    FK_SessionID = activeSession.SessionID,
                    ActionType = "LOGOUT",
                    AffectedEntity = "Session",
                    AffectedRecordID = activeSession.SessionID,
                    Timestamp = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            await _auditService.LogActionAsync("LOGOUT", "User", userId, null, null, userId);
            return ServiceResult<bool>.SuccessResult(true, "Logged out successfully");
        }
    }
}
