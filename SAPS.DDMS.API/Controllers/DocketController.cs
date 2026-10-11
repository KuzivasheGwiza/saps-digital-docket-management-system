// Controllers/DocketController.cs
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
        /// UC 3.2 - Get all dockets (filtered by user role and station)
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
        /// UC 3.2 - Get docket by ID
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
        /// UC 3.1 - Create Docket (with Incident first - business requirement)
        /// Only CSC Officers can create dockets
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
        /// UC 3.3 - Update Docket
        /// Requires reason for update (min 20 characters)
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
        /// UC 3.5 - Allocate Docket to Investigating Officer
        /// Only DSC Supervisors can allocate
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
        /// Get available Investigating Officers for allocation
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
