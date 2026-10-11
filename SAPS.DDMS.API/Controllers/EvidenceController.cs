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
        /// Returns evidence linked to a docket within the user's access scope.
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
        /// Returns an evidence item within the user's access scope.
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
        /// Captures an evidence item and its integrity metadata.
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
        /// Updates an evidence item and records the required audit reason.
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
        /// Returns active evidence types used by capture forms.
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
        /// Returns active evidence storage locations.
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
