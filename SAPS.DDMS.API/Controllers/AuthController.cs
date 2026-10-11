using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Services;


namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Authenticates a user and starts two-factor verification when required.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var result = await _authService.LoginAsync(loginDto, ipAddress);

            if (result == null || !result.Success)
                return Unauthorized(new { success = false, message = result?.Message ?? "Login failed" });

            if (result.Data.RequiresOtp)
            {
                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = result.Message,
                    RequiresOtp = true,
                    PendingPersonnelNumber = result.Data.PendingPersonnelNumber,
                    OtpExpiresAt = result.Data.OtpExpiresAt,
                DeliveryHint = result.Data.DeliveryHint,
                SessionTimeoutMinutes = result.Data.SessionTimeoutMinutes
                });
            }

            if (result.Data.User == null)
                return Unauthorized(new { success = false, message = "Login failed" });

            return Ok(new AuthResponseDto
            {
                Success = true,
                Message = result.Message,
                Token = result.Data.Token,
                Expiration = result.Data.ExpiresAt ?? DateTime.UtcNow.AddMinutes(60),
                SessionTimeoutMinutes = result.Data.SessionTimeoutMinutes,
                User = new UserResponseDto
                {
                    UserID = result.Data.User.Id,
                    FirstName = result.Data.User.FirstName,
                    LastName = result.Data.User.LastName,
                    PersonnelNumber = result.Data.User.PersonnelNumber,
                    Rank = result.Data.User.Rank,
                    EmailAddress = result.Data.User.EmailAddress,
                    PhoneNumber = result.Data.User.PhoneNumber,
                    RoleName = result.Data.User.Role,
                    StationName = result.Data.User.StationName,
                    IsActivated = result.Data.User.IsActivated,
                    IsActive = result.Data.User.IsActive,
                    IsLocked = result.Data.User.IsLocked,
                    DateCreated = result.Data.User.DateCreated,
                    LastLoginDate = result.Data.User.LastLoginDate,
                    Permissions = result.Data.User.Permissions
                }
            });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto verifyOtpDto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var result = await _authService.VerifyOtpAsync(verifyOtpDto, ipAddress);

            if (result == null || !result.Success || result.Data.User == null)
                return Unauthorized(new { success = false, message = result?.Message ?? "OTP verification failed" });

            return Ok(new AuthResponseDto
            {
                Success = true,
                Message = result.Message,
                Token = result.Data.Token,
                Expiration = result.Data.ExpiresAt ?? DateTime.UtcNow.AddMinutes(60),
                SessionTimeoutMinutes = result.Data.SessionTimeoutMinutes,
                User = new UserResponseDto
                {
                    UserID = result.Data.User.Id,
                    FirstName = result.Data.User.FirstName,
                    LastName = result.Data.User.LastName,
                    PersonnelNumber = result.Data.User.PersonnelNumber,
                    Rank = result.Data.User.Rank,
                    EmailAddress = result.Data.User.EmailAddress,
                    PhoneNumber = result.Data.User.PhoneNumber,
                    RoleName = result.Data.User.Role,
                    StationName = result.Data.User.StationName,
                    IsActivated = result.Data.User.IsActivated,
                    IsActive = result.Data.User.IsActive,
                    IsLocked = result.Data.User.IsLocked,
                    DateCreated = result.Data.User.DateCreated,
                    LastLoginDate = result.Data.User.LastLoginDate,
                    Permissions = result.Data.User.Permissions
                }
            });
        }

        /// <summary>
        /// Activates a first-time account and creates its password.
        /// </summary>
        [HttpPost("activate")]
        public async Task<IActionResult> ActivateAccount([FromBody] ActivateAccountDto activateDto)
        {
            var result = await _authService.ActivateAccountAsync(activateDto);

            if (result == null || !result.Success)
                return BadRequest(new { success = false, message = result?.Message ?? "Activation failed" });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Changes the current user's password.
        /// </summary>
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            var userId = GetCurrentUserId();
            var result = await _authService.ChangePasswordAsync(userId, changePasswordDto);

            if (result == null || !result.Success)
                return BadRequest(new { success = false, message = result?.Message ?? "Password change failed" });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Sends a six-digit password-reset PIN to the account email address.
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            var result = await _authService.ForgotPasswordAsync(forgotPasswordDto.Email);

            if (result == null || !result.Success)
                return BadRequest(new { success = false, message = result?.Message ?? "Failed to issue password reset PIN" });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Resets a password after the supplied token is validated.
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            if (resetPasswordDto.NewPassword != resetPasswordDto.ConfirmPassword)
                return BadRequest(new { success = false, message = "Passwords do not match" });

            var result = await _authService.ResetPasswordAsync(resetPasswordDto.Token, resetPasswordDto.NewPassword);

            if (result == null || !result.Success)
                return BadRequest(new { success = false, message = result?.Message ?? "Password reset failed" });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Invalidates the current authenticated session.
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = GetCurrentUserId();
            var result = await _authService.LogoutAsync(userId);

            if (result == null || !result.Success)
                return BadRequest(new { success = false, message = result?.Message ?? "Logout failed" });

            return Ok(new { success = true, message = result.Message });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : 0;
        }

    }
}
