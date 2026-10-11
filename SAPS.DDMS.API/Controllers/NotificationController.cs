using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Services;
using System.Security.Claims;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public NotificationController(ApplicationDbContext db, IAuditService auditService)
        {
            _db = db;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMine([FromQuery] bool unreadOnly = false)
        {
            var userId = GetCurrentUserId();
            var query = _db.Notifications.Where(n => n.FK_UserID == userId);
            if (unreadOnly)
                query = query.Where(n => n.ReadAt == null);

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .Select(n => new
                {
                    n.NotificationID,
                    n.Title,
                    n.Message,
                    n.CreatedAt,
                    n.ReadAt,
                    n.PriorityLevel,
                    IsRead = n.ReadAt != null
                })
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            var count = await _db.Notifications.CountAsync(n => n.FK_UserID == userId && n.ReadAt == null);
            return Ok(new { count });
        }

        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = GetCurrentUserId();
            var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.NotificationID == id && n.FK_UserID == userId);
            if (notification == null) return NotFound(new { success = false, message = "Notification not found." });

            notification.ReadAt ??= DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("READ_NOTIFICATION", "Notification", id, null, new { notification.ReadAt }, userId);

            return Ok(new { success = true, message = "Notification marked as read." });
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
