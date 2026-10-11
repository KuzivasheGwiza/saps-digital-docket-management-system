namespace SAPS.DDMS.API.Models.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; }
        public string? PersonnelNumber { get; set; }
        public string? Rank { get; set; }
        public int ActiveDocketCount { get; set; }
        public int MaximumActiveDockets { get; set; }
        public bool AtCapacity { get; set; }
    }
}
