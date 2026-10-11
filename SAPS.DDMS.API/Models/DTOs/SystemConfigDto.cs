namespace SAPS.DDMS.API.Models.DTOs
{
    public class SystemConfigDto
    {
        public int SessionTimeoutMinutes { get; set; }
        public int OtpExpiryMinutes { get; set; }
        public int DocketInspectionHours { get; set; }
    }

    public class UpdateSystemConfigDto
    {
        public int SessionTimeoutMinutes { get; set; }
        public int OtpExpiryMinutes { get; set; }
        public int DocketInspectionHours { get; set; }
    }

}
