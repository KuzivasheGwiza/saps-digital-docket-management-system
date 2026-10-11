using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models.DTOs
{
    public class PersonDtos
    {
        public class CreatePersonDto
        {
            [Required, StringLength(100, MinimumLength = 2)]
            [RegularExpression(@"^[\p{L}][\p{L}\p{M}' -]*$", ErrorMessage = "First name may contain letters, spaces, apostrophes and hyphens only.")]
            public string FirstName { get; set; } = string.Empty;

            [Required, StringLength(100, MinimumLength = 2)]
            [RegularExpression(@"^[\p{L}][\p{L}\p{M}' -]*$", ErrorMessage = "Last name may contain letters, spaces, apostrophes and hyphens only.")]
            public string LastName { get; set; } = string.Empty;

            [Required, RegularExpression(@"^\d{13}$", ErrorMessage = "ID number must contain exactly 13 digits.")]
            public string IDNumber { get; set; } = string.Empty;

            [Required]
            public DateTime? DateOfBirth { get; set; }

            [Required]
            [RegularExpression(@"^(?:\+27|0)\d{9}$", ErrorMessage = "Phone number must be a South African number such as 0821234567 or +27821234567.")]
            [StringLength(20)]
            public string PhoneNumber { get; set; } = string.Empty;

            [Required]
            [EmailAddress, StringLength(100)]
            public string EmailAddress { get; set; } = string.Empty;

            [Required, StringLength(500, MinimumLength = 5)]
            public string PhysicalAddress { get; set; } = string.Empty;

            [Required, Range(1, int.MaxValue, ErrorMessage = "Select a valid person role.")]
            public int? PersonRoleID { get; set; }
        }

        public class UpdatePersonDto
        {
            [Required, StringLength(100, MinimumLength = 2)]
            [RegularExpression(@"^[\p{L}][\p{L}\p{M}' -]*$", ErrorMessage = "First name may contain letters, spaces, apostrophes and hyphens only.")]
            public string FirstName { get; set; } = string.Empty;

            [Required, StringLength(100, MinimumLength = 2)]
            [RegularExpression(@"^[\p{L}][\p{L}\p{M}' -]*$", ErrorMessage = "Last name may contain letters, spaces, apostrophes and hyphens only.")]
            public string LastName { get; set; } = string.Empty;

            [RegularExpression(@"^(?:\+27|0)\d{9}$", ErrorMessage = "Phone number must be a South African number such as 0821234567 or +27821234567.")]
            [StringLength(20)]
            public string? PhoneNumber { get; set; }

            [EmailAddress, StringLength(100)]
            public string? EmailAddress { get; set; }

            [StringLength(500)]
            public string? PhysicalAddress { get; set; }
        }

        public class LinkPersonToDocketDto
        {
            [Range(1, int.MaxValue)]
            public int PersonID { get; set; }

            [Range(1, int.MaxValue)]
            public int DocketID { get; set; }

            [Required, StringLength(50, MinimumLength = 2)]
            public string RelationshipRole { get; set; } = string.Empty;
        }

        public class PersonDocketLinkDto
        {
            public int DocketID { get; set; }
            public string DocketNumber { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string RelationshipRole { get; set; } = string.Empty;
            public DateTime LinkedAt { get; set; }
        }

        public class PersonResponseDto
        {
            public int PersonID { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string FullName => $"{FirstName} {LastName}".Trim();
            public string? IDNumber { get; set; }
            public string? MaskedIDNumber => string.IsNullOrWhiteSpace(IDNumber)
                ? null
                : IDNumber.Length <= 4
                    ? new string('X', IDNumber.Length)
                    : $"XXX-XXXX-{IDNumber[^4..]}";
            public DateTime? DateOfBirth { get; set; }
            public string? PhoneNumber { get; set; }
            public string? EmailAddress { get; set; }
            public string? PhysicalAddress { get; set; }
            public string? PersonRole { get; set; }
            public DateTime CreatedAt { get; set; }
            public bool IsArchived { get; set; }
            public DateTime? ArchivedAt { get; set; }
            public string? ArchiveReason { get; set; }
            public List<PersonDocketLinkDto> LinkedDockets { get; set; } = new();
        }
    }
}
