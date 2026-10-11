using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;


namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = PermissionCatalog.DocketsView)]
    [Authorize(Roles = "CSC Officer,DSC Supervisor,Investigating Officer,Docket Administrator,Records Clerk,Prosecutor")]
    public class DocketController : ControllerBase
    {
        private readonly IDocketService _docketService;

        public DocketController(IDocketService docketService)
        {
            _docketService = docketService;
        }

        /// <summary>
        /// Returns the dockets visible to the current user's role and station.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDockets([FromQuery] string? status = null)
        {
            var userId = GetCurrentUserId();
            var result = await _docketService.GetDocketsByUserAsync(userId, status);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(result.Data);
        }

        /// <summary>
        /// Returns a docket when it is within the current user's access scope.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocketById(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _docketService.GetDocketByIdAsync(id, userId);

            if (!result.Success)
                return NotFound(new { success = false, message = result.Message });

            return Ok(result.Data);
        }

        /// <summary>
        /// Creates an incident, investigation and docket as one workflow.
        /// Only CSC Officers can create dockets.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "CSC Officer")]
        public async Task<IActionResult> CreateDocket([FromBody] CreateDocketDto createDto)
        {
            var userId = GetCurrentUserId();
            var result = await _docketService.CreateDocketAsync(createDto, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message, docket = result.Data });
        }

        /// <summary>
        /// Updates a docket and records the required audit reason.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "CSC Officer, Docket Administrator")]
        public async Task<IActionResult> UpdateDocket(int id, [FromBody] UpdateDocketDto updateDto)
        {
            var userId = GetCurrentUserId();
            var result = await _docketService.UpdateDocketAsync(id, updateDto, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Allocates an open docket to an eligible Investigating Officer.
        /// </summary>
        [HttpPost("{id}/allocate")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> AllocateDocket(int id, [FromBody] AllocateDocketDto allocateDto)
        {
            var userId = GetCurrentUserId();
            var result = await _docketService.AllocateDocketAsync(id, allocateDto, userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Returns officers who are eligible to receive another docket.
        /// </summary>
        [HttpGet("available-officers")]
        [Authorize(Roles = "DSC Supervisor")]
        public async Task<IActionResult> GetAvailableInvestigatingOfficers()
        {
            var userId = GetCurrentUserId();
            var result = await _docketService.GetAvailableInvestigatingOfficersAsync(userId);

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
