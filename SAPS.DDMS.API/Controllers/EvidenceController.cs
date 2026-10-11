// Controllers/EvidenceController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;
using static SAPS.DDMS.API.Models.DTOs.EvidenceDtos;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = PermissionCatalog.EvidenceView)]
    [Authorize(Roles = "Investigating Officer, DSC Supervisor, Records Clerk")]
    public class EvidenceController : ControllerBase
    {
        private readonly IEvidenceService _evidenceService;

        public EvidenceController(IEvidenceService evidenceService)
        {
            _evidenceService = evidenceService;
        }

        /// <summary>
        /// UC 5.2 - Get evidence by docket ID
        /// </summary>
        [HttpGet("docket/{docketId}")]
        public async Task<IActionResult> GetEvidenceByDocket(int docketId)
        {
            var userId = GetCurrentUserId();
            var result = await _evidenceService.GetEvidenceByDocketAsync(docketId, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(result.Data);
        }

        /// <summary>
        /// UC 5.2 - Get evidence by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEvidenceById(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _evidenceService.GetEvidenceByIdAsync(id, userId);

            if (!result.Success)
                return NotFound(new { success = false, message = result.Message });

            return Ok(result.Data);
        }

        /// <summary>
        /// UC 5.1 - Create evidence item
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Investigating Officer")]
        public async Task<IActionResult> CreateEvidence([FromForm] CreateEvidenceDto createDto)
        {
            var userId = GetCurrentUserId();
            var result = await _evidenceService.CreateEvidenceAsync(createDto, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message, evidence = result.Data });
        }

        /// <summary>
        /// UC 5.3 - Update evidence item
        /// Requires reason (min 10 characters)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Investigating Officer")]
        public async Task<IActionResult> UpdateEvidence(int id, [FromBody] UpdateEvidenceDto updateDto)
        {
            var userId = GetCurrentUserId();
            var result = await _evidenceService.UpdateEvidenceAsync(id, updateDto, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Get all evidence types (for dropdowns)
        /// </summary>
        [HttpGet("types")]
        public async Task<IActionResult> GetEvidenceTypes()
        {
            var result = await _evidenceService.GetEvidenceTypesAsync();
            return result.Success
                ? Ok(result.Data)
                : BadRequest(new { success = false, message = result.Message });
        }

        /// <summary>
        /// Get all evidence locations (for dropdowns)
        /// </summary>
        [HttpGet("locations")]
        public async Task<IActionResult> GetEvidenceLocations()
        {
            var result = await _evidenceService.GetEvidenceLocationsAsync();
            return result.Success
                ? Ok(result.Data)
                : BadRequest(new { success = false, message = result.Message });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : 0;
        }
    }
}
