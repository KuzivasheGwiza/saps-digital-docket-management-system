using System.ComponentModel.DataAnnotations;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Models.DTOs;
using static SAPS.DDMS.API.Models.DTOs.PersonDtos;

namespace SAPS.DDMS.API.Tests.UseCaseTests;

public class ServerValidationTests
{
    [Fact]
    public void PersonDto_RejectsWhitespaceNamesAndMalformedSouthAfricanContactNumber()
    {
        var dto = new CreatePersonDto
        {
            FirstName = "   ",
            LastName = "Valid",
            PhoneNumber = "12345"
        };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(dto.FirstName)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(dto.PhoneNumber)));
    }

    [Fact]
    public void EvidenceDto_RejectsBlankDescriptionAndNonPositiveQuantity()
    {
        var dto = new EvidenceDtos.CreateEvidenceDto
        {
            DocketID = 1,
            Description = "  ",
            EvidenceTypeID = 1,
            EvidenceLocationID = 1,
            DateCollected = DateTime.UtcNow,
            CollectionMethod = "Photographed and bagged",
            StateOfItem = "Sealed",
            SignatureMethod = "Ink",
            Quantity = 0
        };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(dto.Description)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(dto.Quantity)));
    }

    [Fact]
    public void ShortLegacyIdNumber_IsMaskedWithoutThrowing()
    {
        var response = new PersonResponseDto { IDNumber = "123" };

        Assert.Equal("XXX", response.MaskedIDNumber);
    }

    [Fact]
    public void OtpDto_RequiresExactlySixDigits()
    {
        var dto = new VerifyOtpDto { PersonnelNumber = "P001", OtpCode = "12A45" };

        Assert.NotEmpty(Validate(dto));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
