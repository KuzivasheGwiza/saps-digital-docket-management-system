using System.Text.Json;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
namespace SAPS.DDMS.API.Services
{
    public interface IAuditService
    {
        Task LogActionAsync(string actionType, string affectedEntity, int? affectedEntityId,
            object oldValue, object newValue, int userId, string reason = null, string ipAddress = null);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogActionAsync(string actionType, string affectedEntity, int? affectedEntityId,
            object oldValue, object newValue, int userId, string reason = null, string ipAddress = null)
        {
            var auditLog = new UserAuditLog
            {
                ActionType = actionType,
                AffectedEntity = affectedEntity,
                AffectedEntityID = affectedEntityId,
                OldValue = oldValue != null ? JsonSerializer.Serialize(oldValue) : null,
                NewValue = newValue != null ? JsonSerializer.Serialize(newValue) : null,
                Reason = reason,
                IPAddress = ipAddress,
                FK_UserID = userId,
                Timestamp = DateTime.UtcNow
            };

            await _context.UserAuditLogs.AddAsync(auditLog);
            await _context.SaveChangesAsync();
        }
    }
}
