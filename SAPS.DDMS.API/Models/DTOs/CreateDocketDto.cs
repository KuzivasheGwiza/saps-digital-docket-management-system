namespace SAPS.DDMS.API.Models.DTOs
{
    public class CreateDocketDto
    {
        public string? OBReferenceNumber { get; set; }
        public string? FICReferenceNumber { get; set; }
        public string? DocketSummary { get; set; }
        public string? ModusOperandi { get; set; }
        public CreateIncidentDto? Incident { get; set; }
        public string? ComplainantFullName { get; set; }
        public string? ComplainantContactNumber { get; set; }
        public string? CaseType { get; set; }
        public string? SensitivityLevel { get; set; }
    }
}
