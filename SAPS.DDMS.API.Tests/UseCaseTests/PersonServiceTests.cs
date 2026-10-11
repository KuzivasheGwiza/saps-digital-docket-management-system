using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Tests.Support;
using static SAPS.DDMS.API.Models.DTOs.PersonDtos;

namespace SAPS.DDMS.API.Tests.UseCaseTests;

public class PersonServiceTests
{
    [Fact]
    public async Task CreatePerson_RejectsInvalidIdNumber()
    {
        var users = new FakeUserRepository
        {
            UserById = new User
            {
                UserID = 1,
                FK_StationID = 1,
                Role = new Role { Name = "Investigating Officer" }
            }
        };

        var service = new PersonService(
            new FakePersonRepository(),
            new FakeDocketRepository(),
            users,
            new FakeAuditService());

        var result = await service.CreatePersonAsync(new CreatePersonDto
        {
            FirstName = "Amahle",
            LastName = "Zulu",
            IDNumber = "1234"
        }, 1);

        Assert.False(result.Success);
        Assert.Equal("ID number must contain exactly 13 digits.", result.Message);
    }

    [Fact]
    public async Task LinkPersonToDocket_BlocksCscOfficerFromUsingNonComplainantRole()
    {
        var person = new Person
        {
            PersonID = 5,
            FirstName = "Lebo",
            LastName = "Mokoena",
            CreatedByUser = new User { FK_StationID = 1 }
        };

        var personRepository = new FakePersonRepository
        {
            PersonById = person,
            Roles =
            [
                new PersonRole { PersonRoleID = 1, RoleName = "Complainant", IsActive = true },
                new PersonRole { PersonRoleID = 2, RoleName = "Suspect", IsActive = true }
            ]
        };

        var docketRepository = new FakeDocketRepository
        {
            DocketById = new Docket
            {
                DocketID = 9,
                FK_StationID = 1,
                DocketStatus = new DocketStatus { StatusName = "OPEN" }
            }
        };

        var users = new FakeUserRepository
        {
            UserById = new User
            {
                UserID = 2,
                FK_StationID = 1,
                Role = new Role { Name = "CSC Officer" }
            }
        };

        var service = new PersonService(personRepository, docketRepository, users, new FakeAuditService());

        var result = await service.LinkPersonToDocketAsync(5, 9, "Suspect", 2);

        Assert.False(result.Success);
        Assert.Equal("CSC Officers may only link a person as the complainant.", result.Message);
    }
}
