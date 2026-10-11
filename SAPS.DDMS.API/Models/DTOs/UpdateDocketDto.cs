namespace SAPS.DDMS.API.Models.DTOs
{
    public class UpdateDocketDto
    {
        public string? DocketSummary { get; set; }
        public string? ModusOperandi { get; set; }
        public string? SensitivityLevel { get; set; }
        public string? ReasonForUpdate { get; set; }
    }
}
