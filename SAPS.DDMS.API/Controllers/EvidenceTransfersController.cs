using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Services;
using System.Security.Claims;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    public class EvidenceTransfersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;

        public EvidenceTransfersController(ApplicationDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        private int GetCurrentUserId()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "userId" || c.Type == "UserId" || c.Type == "UserID" || c.Type == "sub");
            if (claim == null) throw new UnauthorizedAccessException("User id claim not found");
            return int.Parse(claim.Value);
        }

        public class EvidenceTransferDto
        {
            public int FromLocationID { get; set; }
            public int ToLocationID { get; set; }
            public int ReceivingOfficerUserID { get; set; }
            public string? SignatureMethod { get; set; } // Ink | Stylus | Smartcard
            public string? PayloadHash { get; set; }
            public string? TransferReason { get; set; }
        }

        [HttpPost("api/evidence/{evidenceId}/transfers")]
        [Authorize(Roles = "Records Clerk,Investigating Officer")]
        public async Task<IActionResult> TransferEvidence(int evidenceId, [FromBody] EvidenceTransferDto dto)
        {
            var userId = GetCurrentUserId();

            var requester = await _db.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.UserID == userId);
            if (requester == null) return Unauthorized();

            var evidence = await _db.Evidences
                .Include(e => e.EvidenceLocation)
                .Include(e => e.Docket)
                .FirstOrDefaultAsync(e => e.EvidenceID == evidenceId);
            if (evidence?.Docket == null) return NotFound("Evidence not found.");

            var isSystemAdmin = requester.Role?.Name == "System Administrator";
            if (!isSystemAdmin && requester.FK_StationID != evidence.Docket.FK_StationID)
                return NotFound("Evidence not found.");

            if (requester.Role?.Name == "Investigating Officer")
            {
                var isAssigned = await _db.InvestigationAssignments.AnyAsync(assignment =>
                    assignment.FK_InvestigationID == evidence.FK_InvestigationID &&
                    assignment.FK_UserID == userId);
                if (!isAssigned) return Forbid();
            }

            var fromLoc = await _db.EvidenceLocations.FindAsync(dto.FromLocationID);
            var toLoc = await _db.EvidenceLocations.FindAsync(dto.ToLocationID);
            var receivingOfficer = await _db.Users.FindAsync(dto.ReceivingOfficerUserID);

            if (fromLoc == null || toLoc == null) return BadRequest("Invalid location(s).");
            if (receivingOfficer == null) return BadRequest("Receiving officer not found.");
            if (dto.FromLocationID != evidence.FK_EvidenceLocationID)
                return BadRequest("The source location must match the evidence's current location.");
            if (dto.FromLocationID == dto.ToLocationID)
                return BadRequest("The destination must differ from the current location.");
            if (!receivingOfficer.IsActive || !receivingOfficer.IsActivated || receivingOfficer.IsLocked)
                return BadRequest("The receiving officer must have an active, unlocked account.");
            if (receivingOfficer.FK_StationID != evidence.Docket.FK_StationID)
                return BadRequest("The receiving officer must belong to the docket's station.");
            if (string.IsNullOrWhiteSpace(dto.TransferReason) || dto.TransferReason.Trim().Length < 10)
                return BadRequest("A transfer reason of at least 10 characters is required.");
            if (dto.SignatureMethod is not ("Ink" or "Stylus" or "Smartcard"))
                return BadRequest("Select a valid signature method.");

            var transfer = new EvidenceTransfer
            {
                FK_EvidenceID = evidenceId,
                FromLocationID = dto.FromLocationID,
                ToLocationID = dto.ToLocationID,
                TransferredAt = DateTime.UtcNow,
                TransferredByUserID = userId,
                ReceivingOfficerUserID = dto.ReceivingOfficerUserID,
                SignatureMethod = dto.SignatureMethod,
                PayloadHash = dto.PayloadHash,
                TransferReason = dto.TransferReason
            };

            evidence.FK_EvidenceLocationID = dto.ToLocationID;
            evidence.SignatureMethod = dto.SignatureMethod ?? evidence.SignatureMethod;
            evidence.PayloadHash = dto.PayloadHash ?? evidence.PayloadHash;

            await _db.EvidenceTransfers.AddAsync(transfer);
            await _db.SaveChangesAsync();

            await _audit.LogActionAsync("EVIDENCE_TRANSFER", "Evidence", evidenceId, new { from = fromLoc.LocationName }, new { to = toLoc.LocationName, transfer.TransferredAt }, userId, dto.TransferReason);

            return CreatedAtAction(nameof(GetChainOfCustody), new { evidenceId }, new
            {
                transfer.TransferID,
                EvidenceID = transfer.FK_EvidenceID,
                FromLocation = fromLoc.LocationName,
                ToLocation = toLoc.LocationName,
                transfer.TransferredAt,
                transfer.TransferredByUserID,
                transfer.ReceivingOfficerUserID,
                transfer.SignatureMethod,
                transfer.PayloadHash,
                transfer.TransferReason
            });
        }

        [HttpGet("api/evidence/{evidenceId}/chain-of-custody")]
        [Authorize(Roles = "Records Clerk,Investigating Officer,DSC Supervisor")]
        public async Task<IActionResult> GetChainOfCustody(int evidenceId)
        {
            var userId = GetCurrentUserId();
            var requester = await _db.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.UserID == userId);
            if (requester == null) return Unauthorized();

            var evidence = await _db.Evidences
                .Include(item => item.Docket)
                .FirstOrDefaultAsync(item => item.EvidenceID == evidenceId);
            if (evidence?.Docket == null) return NotFound("Evidence not found.");

            var isSystemAdmin = requester.Role?.Name == "System Administrator";
            if (!isSystemAdmin && requester.FK_StationID != evidence.Docket.FK_StationID)
                return NotFound("Evidence not found.");

            if (requester.Role?.Name == "Investigating Officer")
            {
                var isAssigned = await _db.InvestigationAssignments.AnyAsync(assignment =>
                    assignment.FK_InvestigationID == evidence.FK_InvestigationID &&
                    assignment.FK_UserID == userId);
                if (!isAssigned) return Forbid();
            }

            var transfers = await _db.EvidenceTransfers
                .Include(t => t.FromLocation)
                .Include(t => t.ToLocation)
                .Where(t => t.FK_EvidenceID == evidenceId)
                .OrderBy(t => t.TransferredAt)
                .Select(t => new
                {
                    t.TransferID,
                    t.FK_EvidenceID,
                    TransferDate = t.TransferredAt,
                    TransferredByUserID = t.TransferredByUserID,
                    UserName = _db.Users
                        .Where(u => u.UserID == t.TransferredByUserID)
                        .Select(u => (u.FirstName + " " + u.LastName).Trim())
                        .FirstOrDefault(),
                    ReceivingOfficerUserID = t.ReceivingOfficerUserID,
                    ReceivingOfficerName = _db.Users
                        .Where(u => u.UserID == t.ReceivingOfficerUserID)
                        .Select(u => (u.FirstName + " " + u.LastName).Trim())
                        .FirstOrDefault(),
                    FromLocation = t.FromLocation != null ? t.FromLocation.LocationName : null,
                    ToLocation = t.ToLocation != null ? t.ToLocation.LocationName : null,
                    t.SignatureMethod,
                    t.PayloadHash,
                    t.TransferReason
                })
                .ToListAsync();

            return Ok(transfers);
        }
    }
}
