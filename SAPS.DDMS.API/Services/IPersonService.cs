using SAPS.DDMS.API.Models;

using SAPS.DDMS.API.Repositories;
using SAPS.DDMS.API.Services;

using static SAPS.DDMS.API.Models.DTOs.PersonDtos;

namespace SAPS.DDMS.API.Services
{
    public interface IPersonService
    {
        Task<ServiceResult<PersonResponseDto>> GetPersonByIdAsync(int personId, int requestingUserId);
        Task<ServiceResult<List<PersonResponseDto>>> SearchPersonsAsync(string searchTerm, int requestingUserId);
        Task<ServiceResult<PersonResponseDto>> CreatePersonAsync(CreatePersonDto createDto, int createdByUserId);
        Task<ServiceResult<bool>> UpdatePersonAsync(int personId, UpdatePersonDto updateDto, int requestingUserId);
        Task<ServiceResult<bool>> LinkPersonToDocketAsync(int personId, int docketId, string relationshipRole, int linkedByUserId);
        Task<ServiceResult<List<PersonRole>>> GetPersonRolesAsync();
    }

    public class PersonService : IPersonService
    {
        private readonly IPersonRepository _personRepository;
        private readonly IDocketRepository _docketRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAuditService _auditService;

        public PersonService(
            IPersonRepository personRepository,
            IDocketRepository docketRepository,
            IUserRepository userRepository,
            IAuditService auditService)
        {
            _personRepository = personRepository;
            _docketRepository = docketRepository;
            _userRepository = userRepository;
            _auditService = auditService;
        }

        public async Task<ServiceResult<PersonResponseDto>> GetPersonByIdAsync(int personId, int requestingUserId)
        {
            var requestingUser = await _userRepository.GetByIdAsync(requestingUserId);
            if (requestingUser == null)
                return ServiceResult<PersonResponseDto>.Failure("User not found.");

            var person = await _personRepository.GetByIdAsync(personId);
            if (person == null)
                return ServiceResult<PersonResponseDto>.Failure("Person not found.");

            var links = await _personRepository.GetDocketLinksForPersonAsync(personId);
            var isSystemAdmin = requestingUser.Role?.Name == "System Administrator";
            var stationLinks = links
                .Where(link => link.Docket?.FK_StationID == requestingUser.FK_StationID)
                .ToList();
            var isInScope = isSystemAdmin ||
                            person.CreatedByUser?.FK_StationID == requestingUser.FK_StationID ||
                            stationLinks.Count > 0;
            if (!isInScope)
                return ServiceResult<PersonResponseDto>.Failure("Person not found.");

            var visibleLinks = isSystemAdmin ? links : stationLinks;

            var response = new PersonResponseDto
            {
                PersonID = person.PersonID,
                FirstName = person.FirstName,
                LastName = person.LastName,
                IDNumber = person.IDNumber,
                DateOfBirth = person.DateOfBirth,
                PhoneNumber = person.PhoneNumber,
                EmailAddress = person.EmailAddress,
                PhysicalAddress = person.PhysicalAddress,
                PersonRole = person.PersonRole?.RoleName,
                CreatedAt = person.CreatedAt,
                IsArchived = person.IsArchived,
                ArchivedAt = person.ArchivedAt,
                ArchiveReason = person.ArchiveReason,
                LinkedDockets = visibleLinks.Select(link => new PersonDocketLinkDto
                {
                    DocketID = link.FK_DocketID,
                    DocketNumber = link.Docket?.DocketNumber ?? string.Empty,
                    Status = link.Docket?.DocketStatus?.StatusName ?? string.Empty,
                    RelationshipRole = link.RelationshipRole,
                    LinkedAt = link.LinkedAt
                }).ToList()
            };

            await _auditService.LogActionAsync("READ_PERSON", "Person", personId, null, null, requestingUserId);

            return ServiceResult<PersonResponseDto>.SuccessResult(response);
        }

        public async Task<ServiceResult<List<PersonResponseDto>>> SearchPersonsAsync(string searchTerm, int requestingUserId)
        {
            var requestingUser = await _userRepository.GetByIdAsync(requestingUserId);
            if (requestingUser == null)
                return ServiceResult<List<PersonResponseDto>>.Failure("User not found.");

            var stationScope = requestingUser.Role?.Name == "System Administrator"
                ? 0
                : requestingUser.FK_StationID;
            var persons = await _personRepository.SearchAsync(searchTerm, stationScope);

            var response = persons.Select(p => new PersonResponseDto
            {
                PersonID = p.PersonID,
                FirstName = p.FirstName,
                LastName = p.LastName,
                IDNumber = p.IDNumber,
                PhoneNumber = p.PhoneNumber,
                EmailAddress = p.EmailAddress,
                PersonRole = p.PersonRole?.RoleName,
                CreatedAt = p.CreatedAt,
                IsArchived = p.IsArchived,
                ArchivedAt = p.ArchivedAt,
                ArchiveReason = p.ArchiveReason
            }).ToList();

            return ServiceResult<List<PersonResponseDto>>.SuccessResult(response);
        }

        public async Task<ServiceResult<PersonResponseDto>> CreatePersonAsync(CreatePersonDto createDto, int createdByUserId)
        {
            if (!TryValidate(createDto, out var validationError))
                return ServiceResult<PersonResponseDto>.Failure(validationError);

            var createdBy = await _userRepository.GetByIdAsync(createdByUserId);
            if (createdBy == null)
                return ServiceResult<PersonResponseDto>.Failure("User not found.");

            var idNumber = NormalizeOptional(createDto.IDNumber);
            if (!string.IsNullOrEmpty(idNumber))
            {
                if (idNumber.Length != 13 || !idNumber.All(char.IsDigit))
                    return ServiceResult<PersonResponseDto>.Failure("Invalid ID number format. Must be 13 digits.");

                if (await _personRepository.IdNumberExistsAsync(idNumber))
                    return ServiceResult<PersonResponseDto>.Failure("A person with this ID number is already registered.");
            }

            if (createDto.DateOfBirth.HasValue && createDto.DateOfBirth.Value.Date > DateTime.UtcNow.Date)
                return ServiceResult<PersonResponseDto>.Failure("Date of birth cannot be in the future.");

            if (createDto.DateOfBirth.HasValue && !string.IsNullOrEmpty(idNumber) &&
                !idNumber.StartsWith(createDto.DateOfBirth.Value.ToString("yyMMdd"), StringComparison.Ordinal))
                return ServiceResult<PersonResponseDto>.Failure(
                    "Date of birth must match the first six digits (YYMMDD) of the South African ID number.");

            if (createDto.PersonRoleID.HasValue)
            {
                var roles = await _personRepository.GetPersonRolesAsync();
                if (!roles.Any(role => role.PersonRoleID == createDto.PersonRoleID.Value))
                    return ServiceResult<PersonResponseDto>.Failure("The selected person role does not exist or is inactive.");
            }

            var person = new Person
            {
                FirstName = createDto.FirstName.Trim(),
                LastName = createDto.LastName.Trim(),
                IDNumber = idNumber,
                DateOfBirth = createDto.DateOfBirth,
                PhoneNumber = NormalizeOptional(createDto.PhoneNumber),
                EmailAddress = NormalizeOptional(createDto.EmailAddress)?.ToLowerInvariant(),
                PhysicalAddress = NormalizeOptional(createDto.PhysicalAddress),
                FK_CreatedByUserID = createdByUserId,
                FK_PersonRoleID = createDto.PersonRoleID,
                CreatedAt = DateTime.UtcNow
            };

            await _personRepository.CreateAsync(person);

            var response = new PersonResponseDto
            {
                PersonID = person.PersonID,
                FirstName = person.FirstName,
                LastName = person.LastName,
                IDNumber = person.IDNumber,
                DateOfBirth = person.DateOfBirth,
                PhoneNumber = person.PhoneNumber,
                EmailAddress = person.EmailAddress,
                PhysicalAddress = person.PhysicalAddress
            };

            await _auditService.LogActionAsync("CREATE_PERSON", "Person", person.PersonID, null, response, createdByUserId);

            return ServiceResult<PersonResponseDto>.SuccessResult(response, "Person created successfully.");
        }

        public async Task<ServiceResult<bool>> UpdatePersonAsync(int personId, UpdatePersonDto updateDto, int requestingUserId)
        {
            if (!TryValidate(updateDto, out var validationError))
                return ServiceResult<bool>.Failure(validationError);

            var requestingUser = await _userRepository.GetByIdAsync(requestingUserId);
            if (requestingUser == null)
                return ServiceResult<bool>.Failure("User not found.");

            var person = await _personRepository.GetByIdAsync(personId);
            if (person == null)
                return ServiceResult<bool>.Failure("Person not found.");
            if (person.IsArchived)
                return ServiceResult<bool>.Failure("Archived person records are read-only.");

            var links = await _personRepository.GetDocketLinksForPersonAsync(personId);
            var isSystemAdmin = requestingUser.Role?.Name == "System Administrator";
            var isInScope = isSystemAdmin ||
                            person.CreatedByUser?.FK_StationID == requestingUser.FK_StationID ||
                            links.Any(link => link.Docket?.FK_StationID == requestingUser.FK_StationID);
            if (!isInScope)
                return ServiceResult<bool>.Failure("Person not found.");

            // CSC officers may correct contact details but not identifying information.
            if (requestingUser.Role?.Name == "CSC Officer")
            {
                person.PhoneNumber = NormalizeOptional(updateDto.PhoneNumber);
                person.EmailAddress = NormalizeOptional(updateDto.EmailAddress)?.ToLowerInvariant();
                person.PhysicalAddress = NormalizeOptional(updateDto.PhysicalAddress);
            }
            else
            {
                person.FirstName = updateDto.FirstName.Trim();
                person.LastName = updateDto.LastName.Trim();
                person.PhoneNumber = NormalizeOptional(updateDto.PhoneNumber);
                person.EmailAddress = NormalizeOptional(updateDto.EmailAddress)?.ToLowerInvariant();
                person.PhysicalAddress = NormalizeOptional(updateDto.PhysicalAddress);
            }

            await _personRepository.UpdateAsync(person);

            await _auditService.LogActionAsync("UPDATE_PERSON", "Person", personId, null, updateDto, requestingUserId);

            return ServiceResult<bool>.SuccessResult(true, "Person details updated successfully.");
        }

        public async Task<ServiceResult<bool>> LinkPersonToDocketAsync(int personId, int docketId, string relationshipRole, int linkedByUserId)
        {
            var linkedBy = await _userRepository.GetByIdAsync(linkedByUserId);
            if (linkedBy == null)
                return ServiceResult<bool>.Failure("User not found.");

            var person = await _personRepository.GetByIdAsync(personId);
            if (person == null)
                return ServiceResult<bool>.Failure("Person not found.");
            if (person.IsArchived)
                return ServiceResult<bool>.Failure("Archived person records cannot be linked to a docket.");

            var personLinks = await _personRepository.GetDocketLinksForPersonAsync(personId);
            var isSystemAdmin = linkedBy.Role?.Name == "System Administrator";
            var isPersonInScope = isSystemAdmin ||
                                  person.CreatedByUser?.FK_StationID == linkedBy.FK_StationID ||
                                  personLinks.Any(link => link.Docket?.FK_StationID == linkedBy.FK_StationID);
            if (!isPersonInScope)
                return ServiceResult<bool>.Failure("Person not found.");

            var docket = await _docketRepository.GetByIdAsync(docketId);
            if (docket == null)
                return ServiceResult<bool>.Failure("Docket not found.");

            if (!isSystemAdmin && docket.FK_StationID != linkedBy.FK_StationID)
                return ServiceResult<bool>.Failure("You can only link people to dockets in your assigned station.");

            if (docket.DocketStatus?.StatusName is "CLOSED" or "ARCHIVED")
                return ServiceResult<bool>.Failure("People cannot be linked to a closed or archived docket.");

            var role = relationshipRole?.Trim();
            var allowedRoles = await _personRepository.GetPersonRolesAsync();
            var matchedRole = allowedRoles.FirstOrDefault(item =>
                item.IsActive && string.Equals(item.RoleName, role, StringComparison.OrdinalIgnoreCase));
            if (matchedRole == null)
                return ServiceResult<bool>.Failure("Select a valid relationship role for this docket.");

            if (linkedBy.Role?.Name == "CSC Officer" &&
                !string.Equals(matchedRole.RoleName, "Complainant", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<bool>.Failure("CSC Officers may only link a person as the complainant.");

            if (await _personRepository.IsPersonLinkedToDocketAsync(personId, docketId))
                return ServiceResult<bool>.Failure($"{person.FirstName} {person.LastName} is already linked to this docket.");

            var link = new DocketPersonLink
            {
                FK_DocketID = docketId,
                FK_PersonID = personId,
                FK_LinkedByUserID = linkedByUserId,
                RelationshipRole = matchedRole.RoleName,
                LinkedAt = DateTime.UtcNow
            };

            await _personRepository.LinkPersonToDocketAsync(link);

            await _auditService.LogActionAsync("LINK_PERSON_TO_DOCKET", "DocketPersonLink", link.LinkID,
                null, new { PersonID = personId, DocketID = docketId, link.RelationshipRole }, linkedByUserId);

            return ServiceResult<bool>.SuccessResult(true, $"{person.FirstName} {person.LastName} has been linked to this docket.");
        }

        public async Task<ServiceResult<List<PersonRole>>> GetPersonRolesAsync()
        {
            var roles = await _personRepository.GetPersonRolesAsync();
            return ServiceResult<List<PersonRole>>.SuccessResult(roles);
        }

        private static bool TryValidate(object dto, out string error)
        {
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(dto);
            var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                dto,
                context,
                results,
                validateAllProperties: true);
            error = results.FirstOrDefault()?.ErrorMessage ?? "The supplied person details are invalid.";
            return valid;
        }

        private static string? NormalizeOptional(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
