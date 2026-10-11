// Controllers/IncidentController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Services;
namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IncidentController : ControllerBase
    {
        private readonly IIncidentService _incidentService;

        public IncidentController(IIncidentService incidentService)
        {
            _incidentService = incidentService;
        }

        /// <summary>
        /// Get all incident types (for dropdowns)
        /// </summary>
        [HttpGet("types")]
        public async Task<IActionResult> GetIncidentTypes()
        {
            var result = await _incidentService.GetIncidentTypesAsync();
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(result.Data);
        }

        /// <summary>
        /// Get all geo locations
        /// </summary>
        [HttpGet("locations")]
        public async Task<IActionResult> GetGeoLocations()
        {
            var result = await _incidentService.GetGeoLocationsAsync();
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(result.Data);
        }

        /// <summary>
        /// Get incident by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetIncidentById(int id)
        {
            var result = await _incidentService.GetIncidentByIdAsync(id);
            if (!result.Success)
                return NotFound(new { success = false, message = result.Message });

            return Ok(result.Data);
        }
    }
}