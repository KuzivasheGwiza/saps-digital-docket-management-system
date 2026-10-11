using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SAPS.DDMS.API.Controllers;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Tests.Support;

namespace SAPS.DDMS.API.Tests.UseCaseTests;

public class PersonControllerTests
{
    [Fact]
    public async Task GetPersonRoles_ReturnsRawRoleArray_ForDropdownUseCase()
    {
        var service = new StubPersonService(new List<PersonRole>
        {
            new() { PersonRoleID = 1, RoleName = "Complainant", IsActive = true },
            new() { PersonRoleID = 2, RoleName = "Suspect", IsActive = true }
        });

        var controller = new PersonController(service, db: null!, new FakeAuditService());

        var result = await controller.GetPersonRoles();

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IEnumerable<PersonRole>>(ok.Value);
        Assert.Collection(payload,
            role => Assert.Equal("Complainant", role.RoleName),
            role => Assert.Equal("Suspect", role.RoleName));
    }

    private sealed class StubPersonService : IPersonService
    {
        private readonly List<PersonRole> _roles;

        public StubPersonService(List<PersonRole> roles) => _roles = roles;

        public Task<ServiceResult<PersonDtos.PersonResponseDto>> GetPersonByIdAsync(int personId, int requestingUserId) => throw new NotImplementedException();
        public Task<ServiceResult<List<PersonDtos.PersonResponseDto>>> SearchPersonsAsync(string searchTerm, int requestingUserId) => throw new NotImplementedException();
        public Task<ServiceResult<PersonDtos.PersonResponseDto>> CreatePersonAsync(PersonDtos.CreatePersonDto createDto, int createdByUserId) => throw new NotImplementedException();
        public Task<ServiceResult<bool>> UpdatePersonAsync(int personId, PersonDtos.UpdatePersonDto updateDto, int requestingUserId) => throw new NotImplementedException();
        public Task<ServiceResult<bool>> LinkPersonToDocketAsync(int personId, int docketId, string relationshipRole, int linkedByUserId) => throw new NotImplementedException();
        public Task<ServiceResult<List<PersonRole>>> GetPersonRolesAsync() => Task.FromResult(ServiceResult<List<PersonRole>>.SuccessResult(_roles));
    }
}
