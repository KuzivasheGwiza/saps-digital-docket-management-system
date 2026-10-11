using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = PermissionCatalog.BackupManage)]
    public class BackupController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;
        private readonly IDatabaseBackupService _backupService;

        public BackupController(
            ApplicationDbContext db,
            IAuditService audit,
            IDatabaseBackupService backupService)
        {
            _db = db;
            _audit = audit;
            _backupService = backupService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBackups()
        {
            var records = await _db.BackupRecords
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new
                {
                    b.BackupID,
                    b.FileName,
                    b.CreatedAt,
                    CreatedBy = _db.Users
                        .Where(u => u.UserID == b.CreatedByUserID)
                        .Select(u => u.FirstName + " " + u.LastName)
                        .FirstOrDefault() ?? "System Administrator",
                    SizeMB = Math.Round(b.SizeBytes / 1024m / 1024m, 2),
                    b.Status
                })
                .ToListAsync();

            return Ok(records);
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateBackup()
        {
            var userId = GetCurrentUserId();
            var backup = await _backupService.CreateBackupAsync(userId);

            var record = new BackupRecord
            {
                FileName = backup.FileName,
                FilePath = backup.FilePath,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserID = userId,
                SizeBytes = backup.SizeBytes,
                Status = "Full Snapshot Complete",
                Notes = $"Snapshot package includes {backup.TableCount} tables, {backup.RowCount} rows and {backup.UploadFileCount} uploaded file(s)."
            };

            await _db.BackupRecords.AddAsync(record);
            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("CREATE_BACKUP", "BackupRecord", record.BackupID, null, new { record.FileName, record.SizeBytes }, userId);

            return Ok(ToDto(record, await GetUserName(userId)));
        }

        [HttpPost("restore/{id:int}")]
        public async Task<IActionResult> RestoreBackup(int id)
        {
            var userId = GetCurrentUserId();
            var record = await _db.BackupRecords.AsNoTracking().FirstOrDefaultAsync(item => item.BackupID == id);
            if (record == null)
                return NotFound(new { success = false, message = "Backup record not found." });

            if (!System.IO.File.Exists(record.FilePath))
                return NotFound(new { success = false, message = "Backup file is missing from disk." });

            var restore = await _backupService.RestoreBackupAsync(record.FilePath);

            _db.ChangeTracker.Clear();
            await ClearSessionsAsync();

            var restoreActorId = await ResolveAuditUserIdAsync(userId);
            var restoredRecord = await _db.BackupRecords.FirstOrDefaultAsync(item =>
                item.FilePath == record.FilePath || item.FileName == record.FileName);

            if (restoredRecord == null)
            {
                restoredRecord = new BackupRecord
                {
                    FileName = record.FileName,
                    FilePath = record.FilePath,
                    CreatedAt = record.CreatedAt,
                    CreatedByUserID = record.CreatedByUserID,
                    SizeBytes = record.SizeBytes
                };
                await _db.BackupRecords.AddAsync(restoredRecord);
            }

            restoredRecord.Status = "Restored";
            restoredRecord.RestoredAt = DateTime.UtcNow;
            restoredRecord.RestoredByUserID = restoreActorId;
            restoredRecord.Notes = $"Full restore completed from snapshot package. Restored {restore.TableCount} tables, {restore.RowCount} rows and {restore.UploadFileCount} uploaded file(s). Active sessions were cleared for security.";
            await _db.SaveChangesAsync();

            await _audit.LogActionAsync("RESTORE_BACKUP", "BackupRecord", restoredRecord.BackupID, null, new
            {
                restoredRecord.FileName,
                restoredRecord.Status,
                restore.TableCount,
                restore.RowCount,
                restore.UploadFileCount
            }, restoreActorId);

            return Ok(new
            {
                success = true,
                message = "Backup restored successfully. Operational data has been replaced and all users must sign in again."
            });
        }

        [HttpGet("download/{id:int}")]
        public async Task<IActionResult> DownloadBackup(int id)
        {
            var record = await _db.BackupRecords.FindAsync(id);
            if (record == null || !System.IO.File.Exists(record.FilePath))
                return NotFound(new { success = false, message = "Backup file not found." });

            var bytes = await System.IO.File.ReadAllBytesAsync(record.FilePath);
            var contentType = Path.GetExtension(record.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase)
                ? "application/zip"
                : "application/octet-stream";
            return File(bytes, contentType, record.FileName);
        }

        private object ToDto(BackupRecord record, string createdBy)
        {
            return new
            {
                record.BackupID,
                record.FileName,
                record.CreatedAt,
                CreatedBy = createdBy,
                SizeMB = Math.Round(record.SizeBytes / 1024m / 1024m, 2),
                record.Status,
                record.Notes
            };
        }

        private async Task<string> GetUserName(int userId)
        {
            return await _db.Users
                .Where(u => u.UserID == userId)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync() ?? "System Administrator";
        }

        private async Task<int> ResolveAuditUserIdAsync(int preferredUserId)
        {
            var preferredExists = await _db.Users.AnyAsync(user => user.UserID == preferredUserId);
            if (preferredExists) return preferredUserId;

            var fallback = await _db.Users
                .OrderBy(user => user.UserID)
                .Select(user => user.UserID)
                .FirstOrDefaultAsync();
            if (fallback <= 0)
                throw new InvalidOperationException("The restored backup does not contain any user accounts.");

            return fallback;
        }

        private async Task ClearSessionsAsync()
        {
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM [dbo].[SessionLogs]");
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM [dbo].[Sessions]");
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
