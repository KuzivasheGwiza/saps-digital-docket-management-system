using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;
using System.Security.Claims;
using System.Text;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = PermissionCatalog.AuditView)]
    public class AuditController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;

        public AuditController(ApplicationDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        [HttpGet("logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] string? action,
            [FromQuery] string? entity,
            [FromQuery] int? userId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? q,
            [FromQuery] int pageSize = 150,
            CancellationToken cancellationToken = default)
        {
            pageSize = Math.Clamp(pageSize, 25, 500);
            var query = _db.UserAuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(l => l.ActionType == action);
            if (!string.IsNullOrWhiteSpace(entity))
                query = query.Where(l => l.AffectedEntity == entity);
            if (userId.HasValue)
                query = query.Where(l => l.FK_UserID == userId.Value);
            if (from.HasValue)
                query = query.Where(l => l.Timestamp >= from.Value);
            if (to.HasValue)
                query = query.Where(l => l.Timestamp <= to.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(l =>
                    (l.ActionType != null && l.ActionType.Contains(term)) ||
                    (l.AffectedEntity != null && l.AffectedEntity.Contains(term)) ||
                    (l.Reason != null && l.Reason.Contains(term)));
            }

            var rows = await query
                .OrderByDescending(l => l.Timestamp)
                .Take(pageSize)
                .Select(l => new
                {
                    l.UserAuditLogID,
                    l.ActionType,
                    l.AffectedEntity,
                    l.AffectedEntityID,
                    l.Reason,
                    l.OldValue,
                    l.NewValue,
                    l.Timestamp,
                    l.FK_UserID
                })
                .ToListAsync(cancellationToken);

            // Resolve names separately. On SQL Express, joining a wide audit row
            // (OldValue/NewValue are nvarchar(max)) to Users caused multi-second
            // query plans even for a small result set.
            var userIds = rows.Select(row => row.FK_UserID).Distinct().ToArray();
            var userNames = await _db.Users
                .AsNoTracking()
                .Where(user => userIds.Contains(user.UserID))
                .Select(user => new
                {
                    user.UserID,
                    Name = (user.FirstName + " " + user.LastName).Trim()
                })
                .ToDictionaryAsync(user => user.UserID, user => user.Name, cancellationToken);

            var logs = rows.Select(row => new
            {
                row.UserAuditLogID,
                row.ActionType,
                row.AffectedEntity,
                row.AffectedEntityID,
                row.Reason,
                row.OldValue,
                row.NewValue,
                row.Timestamp,
                row.FK_UserID,
                UserName = userNames.GetValueOrDefault(row.FK_UserID) ?? string.Empty
            });

            return Ok(logs);
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportAuditLogs(
            [FromQuery] string? action,
            [FromQuery] string? entity,
            [FromQuery] int? userId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? q)
        {
            var query = _db.UserAuditLogs.Include(log => log.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(action)) query = query.Where(log => log.ActionType == action);
            if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(log => log.AffectedEntity == entity);
            if (userId.HasValue) query = query.Where(log => log.FK_UserID == userId.Value);
            if (from.HasValue) query = query.Where(log => log.Timestamp >= from.Value);
            if (to.HasValue) query = query.Where(log => log.Timestamp <= to.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(log =>
                    (log.ActionType != null && log.ActionType.Contains(term)) ||
                    (log.AffectedEntity != null && log.AffectedEntity.Contains(term)) ||
                    (log.Reason != null && log.Reason.Contains(term)));
            }

            var rows = await query.OrderByDescending(log => log.Timestamp).Take(10000).ToListAsync();
            static string Csv(object? value)
            {
                var text = value?.ToString() ?? string.Empty;
                if (text.Length > 0 && "=+-@".Contains(text[0]))
                    text = "'" + text;
                return $"\"{text.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ")}\"";
            }

            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,User,Action,Entity,EntityID,Reason,IPAddress,OldValue,NewValue");
            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Csv(row.Timestamp.ToString("O")),
                    Csv(row.User == null ? row.FK_UserID.ToString() : $"{row.User.FirstName} {row.User.LastName}".Trim()),
                    Csv(row.ActionType), Csv(row.AffectedEntity), Csv(row.AffectedEntityID),
                    Csv(row.Reason), Csv(row.IPAddress), Csv(row.OldValue), Csv(row.NewValue)
                }));
            }

            var currentUserValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("userId") ?? User.FindFirstValue("sub");
            if (int.TryParse(currentUserValue, out var currentUserId))
            {
                await _audit.LogActionAsync("EXPORT_AUDIT_LOG", "UserAuditLog", null, null,
                    new { Count = rows.Count, action, entity, userId, from, to, q }, currentUserId);
            }

            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv.ToString());
            return File(bytes, "text/csv; charset=utf-8", $"saps-ddms-audit-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }
    }
}
