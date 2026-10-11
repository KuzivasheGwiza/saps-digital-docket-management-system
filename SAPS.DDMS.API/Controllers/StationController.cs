using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Security;
using SAPS.DDMS.API.Models.DTOs;

namespace SAPS.DDMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
    [Authorize(Policy = PermissionCatalog.StationsView)]
public class StationController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StationController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all stations for dropdown selection, ordered alphabetically by name
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStations()
    {
        var stations = await _context.Stations
            .OrderBy(s => s.StationName)  // Order by Name for better UX
            .Select(s => new StationDto
            {
                Id = s.StationID,
                Name = s.StationName,
                Code = s.Code,
                Province = s.Province ?? "Unknown",
                Region = s.Region ?? ""
            })
            .ToListAsync();

        return Ok(stations);
    }

    /// <summary>
    /// Gets a single station by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetStationById(int id)
    {
        var station = await _context.Stations
            .Where(s => s.StationID == id)
            .Select(s => new StationDto
            {
                Id = s.StationID,
                Name = s.StationName,
                Code = s.Code,
                Province = s.Province ?? "Unknown",
                Region = s.Region ?? ""
            })
            .FirstOrDefaultAsync();

        if (station == null)
            return NotFound(new { Message = "Station not found." });

        return Ok(station);
    }
}
