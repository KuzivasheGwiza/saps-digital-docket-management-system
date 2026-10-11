namespace SAPS.DDMS.API.Models.DTOs
{
    public class CreateIncidentDto
    {
        public DateTime OccurrenceDate { get; set; }
        public string? LocationType { get; set; }
        public string? Description { get; set; }
        public int? IncidentTypeID { get; set; }
        public string? CustomIncidentType { get; set; }
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public string? AreaName { get; set; }
    }
}
