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
    [Route("api/sms-queue")]
    [Authorize(Policy = PermissionCatalog.SmsManage)]
    public class SmsQueueController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;

        public SmsQueueController(ApplicationDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        [HttpGet]
        public async Task<IActionResult> GetQueue([FromQuery] string? status = null)
        {
            var requester = await GetRequesterScopeAsync();
            if (requester == null)
                return Forbid();

            var query = ApplyQueueScope(_db.SmsMessages.AsNoTracking(), requester);
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(s => s.Status == status);

            var rawItems = await query
                .OrderByDescending(s => s.CreatedAt)
                .Take(200)
                .Select(s => new
                {
                    s.SmsMessageID,
                    s.FK_UserID,
                    UserName = s.FK_UserID == null
                        ? null
                        : _db.Users
                            .Where(u => u.UserID == s.FK_UserID)
                            .Select(u => u.FirstName + " " + u.LastName)
                            .FirstOrDefault(),
                    s.RecipientNumber,
                    s.Message,
                    s.Purpose,
                    s.CreatedAt,
                    s.SentAt,
                    s.Status,
                    s.ProviderReference
                })
                .ToListAsync();

            var items = rawItems.Select(s => new
            {
                s.SmsMessageID,
                s.FK_UserID,
                s.UserName,
                RecipientNumber = MaskRecipientNumber(s.RecipientNumber),
                s.Message,
                s.Purpose,
                s.CreatedAt,
                s.SentAt,
                s.Status,
                s.ProviderReference
            });

            return Ok(items);
        }

        [HttpPatch("{id:int}/dispatch")]
        public async Task<IActionResult> MarkDispatched(int id)
        {
            var requester = await GetRequesterScopeAsync();
            if (requester == null)
                return Forbid();

            var sms = await ApplyQueueScope(_db.SmsMessages, requester)
                .SingleOrDefaultAsync(item => item.SmsMessageID == id);
            if (sms == null)
                return NotFound(new { success = false, message = "SMS queue item not found." });
            if (!sms.Status.StartsWith("Queued", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Only queued SMS items can be marked as dispatched." });
            if (string.IsNullOrWhiteSpace(sms.RecipientNumber))
                return BadRequest(new { success = false, message = "A recipient number is required before dispatch." });

            var old = new { sms.Status, sms.SentAt };
            sms.Status = "Sent - Manual Dispatch";
            sms.SentAt = DateTime.UtcNow;
            sms.ProviderReference ??= $"MANUAL-{Guid.NewGuid():N}"[..20];
            await _db.SaveChangesAsync();

            await _audit.LogActionAsync("SMS_MARK_DISPATCHED", "SmsMessage", id, old, new { sms.Status, sms.SentAt, sms.ProviderReference }, GetCurrentUserId());
            return Ok(new { success = true, message = "SMS marked as dispatched." });
        }

        [HttpPatch("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var requester = await GetRequesterScopeAsync();
            if (requester == null)
                return Forbid();

            var sms = await ApplyQueueScope(_db.SmsMessages, requester)
                .SingleOrDefaultAsync(item => item.SmsMessageID == id);
            if (sms == null)
                return NotFound(new { success = false, message = "SMS queue item not found." });
            if (!sms.Status.StartsWith("Queued", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Only queued SMS items can be cancelled." });

            var old = new { sms.Status, sms.SentAt };
            sms.Status = "Cancelled";
            sms.SentAt = null;
            await _db.SaveChangesAsync();

            await _audit.LogActionAsync("SMS_CANCELLED", "SmsMessage", id, old, new { sms.Status }, GetCurrentUserId());
            return Ok(new { success = true, message = "SMS cancelled." });
        }

        private IQueryable<SmsMessage> ApplyQueueScope(IQueryable<SmsMessage> query, RequesterScope requester)
        {
            // Authentication messages must never appear in or be mutated through
            // the operational queue.
            query = query.Where(message => !EF.Functions.Like(message.Purpose, "%OTP%"));

            if (!requester.IsSystemAdministrator)
            {
                var stationId = requester.StationId;
                query = query.Where(message =>
                    message.FK_UserID.HasValue &&
                    _db.Users.Any(user =>
                        user.UserID == message.FK_UserID.Value &&
                        user.FK_StationID == stationId));
            }

            return query;
        }

        private async Task<RequesterScope?> GetRequesterScopeAsync()
        {
            var stationId = await _db.Users
                .AsNoTracking()
                .Where(user => user.UserID == GetCurrentUserId())
                .Select(user => (int?)user.FK_StationID)
                .SingleOrDefaultAsync();

            return stationId.HasValue
                ? new RequesterScope(stationId.Value, User.IsInRole("System Administrator"))
                : null;
        }

        private static string MaskRecipientNumber(string? recipientNumber)
        {
            if (string.IsNullOrWhiteSpace(recipientNumber))
                return string.Empty;

            var value = recipientNumber.Trim();
            if (value.Length <= 4)
                return new string('*', value.Length);

            return new string('*', value.Length - 4) + value[^4..];
        }

        private sealed record RequesterScope(int StationId, bool IsSystemAdministrator);

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : 0;
        }
    }
}
