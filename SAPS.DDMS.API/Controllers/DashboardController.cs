using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = PermissionCatalog.DashboardView)]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// Returns the aggregated statistics used by the dashboard charts.
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var userId = GetCurrentUserId();
            var result = await _dashboardService.GetDashboardStatsAsync(userId);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(result.Data);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : 0;
        }
    }
}
