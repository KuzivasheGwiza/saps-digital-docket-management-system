// Controllers/PersonController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;
using System.Security.Claims;
using static SAPS.DDMS.API.Models.DTOs.PersonDtos;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = PermissionCatalog.PersonsView)]
    public class PersonController : ControllerBase
    {
        private readonly IPersonService _personService;
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public PersonController(IPersonService personService, ApplicationDbContext db, IAuditService auditService)
        {
            _personService = personService;
            _db = db;
            _auditService = auditService;
        }

        /// <summary>
        /// UC 11.2 - Search persons
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchPersons([FromQuery] string? searchTerm = null)
        {
            var userId = GetCurrentUserId();
            var result = await _personService.SearchPersonsAsync(searchTerm ?? string.Empty, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(result.Data);
        }

        /// <summary>
        /// UC 11.2 - Get person by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPersonById(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _personService.GetPersonByIdAsync(id, userId);

            if (!result.Success)
                return NotFound(new { success = false, message = result.Message });

            return Ok(result.Data);
        }

        /// <summary>
        /// UC 11.1 - Create person details
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "System Administrator, CSC Officer, Investigating Officer, DSC Supervisor, Docket Administrator")]
        public async Task<IActionResult> CreatePerson([FromBody] CreatePersonDto createDto)
        {
            var userId = GetCurrentUserId();
            var result = await _personService.CreatePersonAsync(createDto, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message, personId = result.Data?.PersonID });
        }

        /// <summary>
        /// UC 11.3 - Update person details
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "System Administrator, CSC Officer, Investigating Officer, DSC Supervisor, Docket Administrator")]
        public async Task<IActionResult> UpdatePerson(int id, [FromBody] UpdatePersonDto updateDto)
        {
            var userId = GetCurrentUserId();
            var result = await _personService.UpdatePersonAsync(id, updateDto, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// UC 11.5 - Link person to docket
        /// </summary>
        [HttpPost("link")]
        [Authorize(Roles = "System Administrator, CSC Officer, Investigating Officer, DSC Supervisor")]
        public async Task<IActionResult> LinkPersonToDocket([FromBody] LinkPersonToDocketDto linkDto)
        {
            var userId = GetCurrentUserId();
            var result = await _personService.LinkPersonToDocketAsync(
                linkDto.PersonID, linkDto.DocketID, linkDto.RelationshipRole, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        public class ArchivePersonDto
        {
            public string Reason { get; set; } = string.Empty;
        }

        [HttpPatch("{id}/archive")]
        [Authorize(Roles = "System Administrator, Investigating Officer, DSC Supervisor, Docket Administrator")]
        public async Task<IActionResult> ArchivePerson(int id, [FromBody] ArchivePersonDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Reason) || dto.Reason.Trim().Length < 10)
                return BadRequest(new { success = false, message = "Archive reason must be at least 10 characters." });

            var userId = GetCurrentUserId();
            var requester = await _db.Users
                .Include(user => user.Role)
                .FirstOrDefaultAsync(user => user.UserID == userId);
            if (requester == null) return Unauthorized();

            var person = await _db.Persons
                .Include(p => p.CreatedByUser)
                .Include(p => p.DocketPersonLinks)
                    .ThenInclude(link => link.Docket)
                .FirstOrDefaultAsync(p => p.PersonID == id);
            if (person == null) return NotFound(new { success = false, message = "Person not found." });

            var isSystemAdmin = requester.Role?.Name == "System Administrator";
            var isInScope = isSystemAdmin ||
                            person.CreatedByUser?.FK_StationID == requester.FK_StationID ||
                            person.DocketPersonLinks.Any(link => link.Docket?.FK_StationID == requester.FK_StationID);
            if (!isInScope)
                return NotFound(new { success = false, message = "Person not found." });

            if (person.IsArchived)
                return BadRequest(new { success = false, message = "Person is already archived." });

            person.IsArchived = true;
            person.ArchivedAt = DateTime.UtcNow;
            person.ArchivedByUserID = userId;
            person.ArchiveReason = dto.Reason.Trim();
            var archive = new PersonArchive
            {
                FK_PersonID = id,
                Reason = person.ArchiveReason,
                ArchivedByUserID = userId,
                ArchivedAt = person.ArchivedAt.Value
            };
            await _db.PersonArchives.AddAsync(archive);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("ARCHIVE_PERSON", "Person", id, null, archive, userId, dto.Reason);

            return Ok(new { success = true, message = "Person archived." });
        }

        /// <summary>
        /// Get all person roles (for dropdowns)
        /// </summary>
        [HttpGet("roles")]
        public async Task<IActionResult> GetPersonRoles()
        {
            var result = await _personService.GetPersonRolesAsync();
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(result.Data);
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
