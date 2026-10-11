namespace SAPS.DDMS.API.Models.DTOs;

public class StationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Province { get; set; }
    public string Region { get; set; }
}