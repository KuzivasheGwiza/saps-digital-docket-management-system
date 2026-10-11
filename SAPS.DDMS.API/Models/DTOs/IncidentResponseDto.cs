namespace SAPS.DDMS.API.Models.DTOs
{
    public class IncidentResponseDto
    {
        public int IncidentID { get; set; }
        public DateTime OccurrenceDate { get; set; }
        public string LocationType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IncidentType { get; set; } = string.Empty;
        public string AreaName { get; set; } = string.Empty;
        public string Latitude { get; set; } = string.Empty;
        public string Longitude { get; set; } = string.Empty;
    }
}
