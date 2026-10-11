using SAPS.DDMS.API.Models;
// Services/IDocketService.cs
using SAPS.DDMS.API.Helpers;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Repositories;
using SAPS.DDMS.API.Data;


namespace SAPS.DDMS.API.Services
{
    public interface IDocketService
    {
        Task<ServiceResult<DocketResponseDto>> CreateDocketAsync(CreateDocketDto createDto, int createdByUserId);
        Task<ServiceResult<DocketResponseDto>> GetDocketByIdAsync(int docketId, int requestingUserId);
        Task<ServiceResult<List<DocketResponseDto>>> GetDocketsByUserAsync(int userId, string status);
        Task<ServiceResult<bool>> UpdateDocketAsync(int docketId, UpdateDocketDto updateDto, int requestingUserId);
        Task<ServiceResult<bool>> AllocateDocketAsync(int docketId, AllocateDocketDto allocateDto, int allocatedByUserId);
        Task<ServiceResult<List<UserDto>>> GetAvailableInvestigatingOfficersAsync(int userId);
    }

    public class DocketService : IDocketService
    {
        private sealed class DocketListRow
        {
            public int DocketID { get; set; }
            public int? InvestigationID { get; set; }
            public string? DocketNumber { get; set; }
            public string? OBReferenceNumber { get; set; }
            public string? FICReferenceNumber { get; set; }
            public string? DocketSummary { get; set; }
            public string? ModusOperandi { get; set; }
            public string? PriorityLevel { get; set; }
            public string? SensitivityLevel { get; set; }
            public string? CaseType { get; set; }
            public string? ComplainantFullName { get; set; }
            public string? ComplainantContactNumber { get; set; }
            public string? Status { get; set; }
            public DateTime DateCreated { get; set; }
            public DateTime? DateAllocated { get; set; }
            public DateTime? DocketArchivedAt { get; set; }
            public DateTime? DocketClosedAt { get; set; }
            public DateTime? ReopenedAt { get; set; }
            public string? CrimeCategory { get; set; }
            public string? StationName { get; set; }
            public string? StationRegion { get; set; }
            public string? StationProvince { get; set; }
            public int CreatedByUserID { get; set; }
            public string? CreatedByFirstName { get; set; }
            public string? CreatedByLastName { get; set; }
            public int? IncidentID { get; set; }
            public DateTime? IncidentOccurrenceDate { get; set; }
            public string? IncidentLocationType { get; set; }
            public string? IncidentDescription { get; set; }
            public string? IncidentType { get; set; }
            public string? IncidentAreaName { get; set; }
            public string? IncidentLatitude { get; set; }
            public string? IncidentLongitude { get; set; }
        }

        private sealed class DocketAssignmentRow
        {
            public int InvestigationID { get; set; }
            public DateTime DateAssigned { get; set; }
            public int AssignmentID { get; set; }
            public DateTime? DateAccepted { get; set; }
            public DateTime? InspectionDueAt { get; set; }
            public DateTime? InspectionCompletedAt { get; set; }
            public string? AllocationReviewStatus { get; set; }
            public string? AllocationReviewNotes { get; set; }
            public int AssignedOfficerUserID { get; set; }
            public string? UserFirstName { get; set; }
            public string? UserLastName { get; set; }
        }

        private sealed class DocketSuspectRow
        {
            public int DocketID { get; set; }
            public string? RelationshipRole { get; set; }
            public string? PersonRoleCode { get; set; }
            public string? PersonRoleName { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
        }

        private readonly IDocketRepository _docketRepository;
        private readonly IIncidentRepository _incidentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPersonRepository _personRepository;
        private readonly IAuditService _auditService;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly INotificationRepository _notificationRepository;
        private readonly ApplicationDbContext _db;

        public DocketService(
            IDocketRepository docketRepository,
            IIncidentRepository incidentRepository,
            IUserRepository userRepository,
            IPersonRepository personRepository,
            IAuditService auditService,
            IEmailService emailService,
            ISmsService smsService,
            INotificationRepository notificationRepository,
            ApplicationDbContext db)
        {
            _docketRepository = docketRepository;
            _incidentRepository = incidentRepository;
            _userRepository = userRepository;
            _personRepository = personRepository;
            _auditService = auditService;
            _emailService = emailService;
            _smsService = smsService;
            _notificationRepository = notificationRepository;
            _db = db;
        }

        private bool ValidateFICRequirement(string caseType, string ficReferenceNumber)
        {
            if (caseType == "Financial Crime")
            {
                return !string.IsNullOrWhiteSpace(ficReferenceNumber);
            }
            return true;
        }

        private async Task<int> GetMaxActiveDocketsPerOfficerAsync()
        {
            var configuredValue = await _db.SystemSettings
                .AsNoTracking()
                .Where(setting => setting.SettingKey == "MAX_ACTIVE_DOCKETS_PER_OFFICER")
                .Select(setting => setting.SettingValue)
                .FirstOrDefaultAsync();

            return int.TryParse(configuredValue, out var parsed) && parsed > 0
                ? parsed
                : 10;
        }

        private async Task<Dictionary<int, int>> GetOfficerActiveDocketCountsAsync(IEnumerable<int> officerIds)
        {
            var officerIdList = officerIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (!officerIdList.Any())
                return new Dictionary<int, int>();

            var latestAssignments = (await _db.InvestigationAssignments
                    .AsNoTracking()
                    .Where(assignment => officerIdList.Contains(assignment.FK_UserID))
                    .Select(assignment => new
                    {
                        assignment.FK_InvestigationID,
                        assignment.FK_UserID,
                        assignment.DateAssigned,
                        assignment.AssignmentID
                    })
                    .ToListAsync())
                .GroupBy(assignment => assignment.FK_InvestigationID)
                .Select(group => group
                    .OrderByDescending(assignment => assignment.DateAssigned)
                    .ThenByDescending(assignment => assignment.AssignmentID)
                    .First())
                .ToList();

            if (!latestAssignments.Any())
                return new Dictionary<int, int>();

            var investigationIds = latestAssignments
                .Select(assignment => assignment.FK_InvestigationID)
                .Distinct()
                .ToList();

            var activeInvestigationIds = (await (
                    from docket in _db.Dockets.AsNoTracking()
                    join status in _db.DocketStatuses.AsNoTracking()
                        on docket.FK_DocketStatusID equals status.DocketStatusID
                    where docket.FK_InvestigationID.HasValue &&
                          investigationIds.Contains(docket.FK_InvestigationID.Value) &&
                          status.StatusName != "OPEN" &&
                          status.StatusName != "CLOSED" &&
                          status.StatusName != "ARCHIVED"
                    select docket.FK_InvestigationID!.Value)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            return latestAssignments
                .Where(assignment => activeInvestigationIds.Contains(assignment.FK_InvestigationID))
                .GroupBy(assignment => assignment.FK_UserID)
                .ToDictionary(group => group.Key, group => group.Count());
        }

        private async Task<int> GetOfficerActiveDocketCountAsync(int officerUserId)
        {
            var counts = await GetOfficerActiveDocketCountsAsync(new[] { officerUserId });
            return counts.TryGetValue(officerUserId, out var count) ? count : 0;
        }

        private static DocketResponseDto MapToDocketResponse(Docket docket)
        {
            var assignment = docket.Investigation?.InvestigationAssignments?
                .OrderByDescending(a => a.DateAssigned)
                .FirstOrDefault();
            if (string.Equals(assignment?.AllocationReviewStatus, "Unallocated", StringComparison.OrdinalIgnoreCase))
                assignment = null;

            var suspect = docket.DocketPersonLinks?
                .Where(l => l.Person != null && !l.Person.IsArchived)
                .FirstOrDefault(l =>
                    string.Equals(l.RelationshipRole, "Suspect", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(l.Person.PersonRole?.Code, "SUS", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(l.Person.PersonRole?.RoleName, "Suspect", StringComparison.OrdinalIgnoreCase))
                ?.Person;

            var jurisdictionParts = new[] { docket.Station?.Region, docket.Station?.Province }
                .Where(v => !string.IsNullOrWhiteSpace(v));

            return new DocketResponseDto
            {
                DocketID = docket.DocketID,
                InvestigationID = docket.FK_InvestigationID,
                DocketNumber = docket.DocketNumber ?? string.Empty,
                OBReferenceNumber = docket.OBReferenceNumber ?? string.Empty,
                FICReferenceNumber = docket.FICReferenceNumber ?? string.Empty,
                DocketSummary = docket.DocketSummary ?? string.Empty,
                ModusOperandi = docket.ModusOperandi ?? string.Empty,
                PriorityLevel = docket.PriorityLevel ?? string.Empty,
                SensitivityLevel = docket.SensitivityLevel ?? "NORMAL",
                CaseType = docket.CaseType ?? string.Empty,
                ComplainantFullName = docket.ComplainantFullName ?? string.Empty,
                ComplainantContactNumber = docket.ComplainantContactNumber ?? string.Empty,
                Status = docket.DocketStatus?.StatusName ?? string.Empty,
                DateCreated = docket.DateCreated,
                DateAllocated = docket.DateAllocated,
                AssignmentAcceptedAt = assignment?.DateAccepted,
                InspectionDueAt = assignment?.InspectionDueAt,
                InspectionCompletedAt = assignment?.InspectionCompletedAt,
                AllocationReviewStatus = assignment?.AllocationReviewStatus ?? string.Empty,
                AllocationReviewNotes = assignment?.AllocationReviewNotes ?? string.Empty,
                DocketArchivedAt = docket.DocketArchivedAt,
                DocketClosedAt = docket.DocketClosedAt,
                ReopenedAt = docket.ReopenedAt,
                CrimeCategory = docket.Incident?.IncidentType?.TypeName ?? docket.Incident?.CustomIncidentType ?? string.Empty,
                StationName = docket.Station?.StationName ?? string.Empty,
                Jurisdiction = string.Join(", ", jurisdictionParts),
                SuspectName = suspect != null ? $"{suspect.FirstName} {suspect.LastName}".Trim() : string.Empty,
                PrivacyFlag = docket.DocketPrivacyFlags?.Any(f => f.RemovedAt == null) == true,
                AssignedOfficerUserID = assignment?.FK_UserID,
                AssignedOfficerName = assignment?.User != null ? $"{assignment.User.FirstName} {assignment.User.LastName}".Trim() : string.Empty,
                CreatedByUserID = docket.FK_CreatedByUserID,
                Incident = docket.Incident != null ? new IncidentResponseDto
                {
                    IncidentID = docket.Incident.IncidentID,
                    OccurrenceDate = docket.Incident.OccurrenceDate,
                    LocationType = docket.Incident.LocationType ?? string.Empty,
                    Description = docket.Incident.Description ?? string.Empty,
                    IncidentType = docket.Incident.IncidentType?.TypeName ?? docket.Incident.CustomIncidentType ?? string.Empty,
                    AreaName = docket.Incident.GeoLocation?.AreaName ?? string.Empty,
                    Latitude = docket.Incident.GeoLocation?.Latitude ?? string.Empty,
                    Longitude = docket.Incident.GeoLocation?.Longitude ?? string.Empty
                } : null,
                CreatedByName = docket.CreatedByUser != null ? $"{docket.CreatedByUser.FirstName} {docket.CreatedByUser.LastName}".Trim() : string.Empty
            };
        }

        // UC 3.1 - Create Docket (Incident must be captured first)
        public async Task<ServiceResult<DocketResponseDto>> CreateDocketAsync(CreateDocketDto createDto, int createdByUserId)
        {
            var createdByUser = await _userRepository.GetByIdAsync(createdByUserId);
            if (createdByUser == null)
                return ServiceResult<DocketResponseDto>.Failure("User not found.");

            if (createDto.Incident == null)
                return ServiceResult<DocketResponseDto>.Failure("Incident details are required.");

            if (createDto.Incident.OccurrenceDate == default)
                return ServiceResult<DocketResponseDto>.Failure("Incident date is required.");

            if (string.IsNullOrWhiteSpace(createDto.Incident.Description))
                return ServiceResult<DocketResponseDto>.Failure("Incident description is required.");

            if (string.IsNullOrWhiteSpace(createDto.Incident.LocationType) || createDto.Incident.LocationType.Trim().Length > 100)
                return ServiceResult<DocketResponseDto>.Failure("Location type is required and must not exceed 100 characters.");

            if (!createDto.Incident.IncidentTypeID.HasValue && string.IsNullOrWhiteSpace(createDto.Incident.CustomIncidentType))
                return ServiceResult<DocketResponseDto>.Failure("Select an incident type or specify the other incident type.");

            if (string.IsNullOrWhiteSpace(createDto.ComplainantFullName))
                return ServiceResult<DocketResponseDto>.Failure("Complainant full name is required.");

            var sensitivityLevel = (createDto.SensitivityLevel ?? "NORMAL").Trim().ToUpperInvariant();
            if (sensitivityLevel is not ("NORMAL" or "RESTRICTED" or "HIGHLY_SENSITIVE"))
                return ServiceResult<DocketResponseDto>.Failure("Sensitivity level must be Normal, Restricted or Highly Sensitive.");

            var generatedAt = DateTime.UtcNow;
            var obReferenceNumber = DocketNumberHelper.GenerateOBReferenceNumber(createdByUser.FK_StationID, generatedAt);
            for (var attempt = 0; attempt < 5 && await _docketRepository.OBReferenceExistsAsync(obReferenceNumber); attempt++)
                obReferenceNumber = DocketNumberHelper.GenerateOBReferenceNumber(createdByUser.FK_StationID, generatedAt);
            if (await _docketRepository.OBReferenceExistsAsync(obReferenceNumber))
                return ServiceResult<DocketResponseDto>.Failure("The OB reference could not be generated because a unique value was not available. Please try again.");
            var ficReferenceNumber = DocketNumberHelper.GenerateFICReferenceNumber(createdByUser.FK_StationID, generatedAt);

            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Step 1: Create Incident FIRST (business requirement - incident must be captured before docket)
            var incident = new Incident
            {
                OccurrenceDate = createDto.Incident.OccurrenceDate,
                LocationType = createDto.Incident.LocationType,
                Description = createDto.Incident.Description,
                CustomIncidentType = createDto.Incident.CustomIncidentType?.Trim(),
                FK_UserID = createdByUserId
            };

            if (createDto.Incident.IncidentTypeID.HasValue)
                incident.FK_IncidentTypeID = createDto.Incident.IncidentTypeID;

            if (!string.IsNullOrEmpty(createDto.Incident.Latitude))
            {
                var geoLocation = new GeoLocation
                {
                    Latitude = createDto.Incident.Latitude,
                    Longitude = createDto.Incident.Longitude,
                    AreaName = createDto.Incident.AreaName
                };
                var savedGeo = await _incidentRepository.CreateGeoLocationAsync(geoLocation);
                incident.FK_GeoID = savedGeo.GeoID;
            }

            var createdIncident = await _incidentRepository.CreateIncidentAsync(incident);

            // Step 2: Generate docket number
            var docketNumber = DocketNumberHelper.GenerateDocketNumber(createdByUser.FK_StationID, generatedAt);

            // Step 3: Create Investigation
            var investigation = new Investigation
            {
                Title = $"Investigation for Docket {docketNumber}",
                FK_InvestigationStatusID = 1 // OPEN
            };
            var createdInvestigation = await _docketRepository.CreateInvestigationAsync(investigation);

            // Step 4: Create Docket
            var docket = new Docket
            {
                DocketNumber = docketNumber,
                OBReferenceNumber = obReferenceNumber,
                FICReferenceNumber = ficReferenceNumber,
                DocketSummary = createDto.DocketSummary,
                ModusOperandi = createDto.ModusOperandi,
                ComplainantFullName = createDto.ComplainantFullName,
                ComplainantContactNumber = createDto.ComplainantContactNumber,
                CaseType = createDto.CaseType,
                PriorityLevel = "NORMAL",
                SensitivityLevel = sensitivityLevel,
                IncidentDate = createDto.Incident.OccurrenceDate,
                FK_DocketStatusID = 1, // OPEN
                FK_StationID = createdByUser.FK_StationID,
                FK_CreatedByUserID = createdByUserId,
                FK_IncidentID = createdIncident.IncidentID,
                FK_InvestigationID = createdInvestigation.InvestigationID
            };

            await _docketRepository.CreateAsync(docket);

            await _auditService.LogActionAsync("CREATE_DOCKET", "Docket", docket.DocketID, null,
                new { docket.DocketNumber, docket.OBReferenceNumber }, createdByUserId);

            await transaction.CommitAsync();

            var savedDocket = await _docketRepository.GetByIdWithDetailsAsync(docket.DocketID) ?? docket;
            var response = MapToDocketResponse(savedDocket);

            return ServiceResult<DocketResponseDto>.SuccessResult(response, $"Docket created successfully. Number: {docketNumber}");
        }

        // UC 3.2 - Read Docket by ID
        public async Task<ServiceResult<DocketResponseDto>> GetDocketByIdAsync(int docketId, int requestingUserId)
        {
            var requestingUser = await _userRepository.GetByIdAsync(requestingUserId);
            if (requestingUser == null)
                return ServiceResult<DocketResponseDto>.Failure("User not found.");

            var docket = await _docketRepository.GetByIdWithDetailsAsync(docketId);
            if (docket == null)
                return ServiceResult<DocketResponseDto>.Failure("Docket not found.");

            // Station scope access control
            if (docket.FK_StationID != requestingUser.FK_StationID && requestingUser.Role?.Name != "System Administrator")
                return ServiceResult<DocketResponseDto>.Failure("You do not have permission to view this docket.");

            var response = MapToDocketResponse(docket);

            await _auditService.LogActionAsync("READ_DOCKET", "Docket", docketId, null, null, requestingUserId);

            return ServiceResult<DocketResponseDto>.SuccessResult(response);
        }

        public async Task<ServiceResult<List<DocketResponseDto>>> GetDocketsByUserAsync(int userId, string status)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return ServiceResult<List<DocketResponseDto>>.Failure("User not found.");

            IQueryable<Docket> query = _db.Dockets.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(d => d.DocketStatus != null && d.DocketStatus.StatusName == status);

            if (user.Role?.Name == "Investigating Officer")
            {
                var assignedInvestigationIds = _db.InvestigationAssignments
                    .AsNoTracking()
                    .Where(assignment => assignment.FK_UserID == userId &&
                        assignment.AllocationReviewStatus != "Unallocated" &&
                        !_db.InvestigationAssignments.Any(other =>
                            other.FK_InvestigationID == assignment.FK_InvestigationID &&
                            (other.DateAssigned > assignment.DateAssigned ||
                             (other.DateAssigned == assignment.DateAssigned && other.AssignmentID > assignment.AssignmentID))))
                    .Select(assignment => assignment.FK_InvestigationID)
                    .Distinct();

                query = query
                    .Where(d => d.FK_InvestigationID.HasValue && assignedInvestigationIds.Contains(d.FK_InvestigationID.Value))
                    .OrderByDescending(d => d.DateAllocated ?? d.DateCreated);
            }
            else if (user.Role?.Name == "System Administrator")
            {
                query = query.OrderByDescending(d => d.DateCreated);
            }
            else
            {
                query = query
                    .Where(d => d.FK_StationID == user.FK_StationID)
                    .OrderByDescending(d => d.DateCreated);
            }

            var dockets = await query
                .Select(d => new DocketListRow
                {
                    DocketID = d.DocketID,
                    InvestigationID = d.FK_InvestigationID,
                    DocketNumber = d.DocketNumber,
                    OBReferenceNumber = d.OBReferenceNumber,
                    FICReferenceNumber = d.FICReferenceNumber,
                    DocketSummary = d.DocketSummary,
                    ModusOperandi = d.ModusOperandi,
                    PriorityLevel = d.PriorityLevel,
                    SensitivityLevel = d.SensitivityLevel,
                    CaseType = d.CaseType,
                    ComplainantFullName = d.ComplainantFullName,
                    ComplainantContactNumber = d.ComplainantContactNumber,
                    Status = d.DocketStatus != null ? d.DocketStatus.StatusName : null,
                    DateCreated = d.DateCreated,
                    DateAllocated = d.DateAllocated,
                    DocketArchivedAt = d.DocketArchivedAt,
                    DocketClosedAt = d.DocketClosedAt,
                    ReopenedAt = d.ReopenedAt,
                    CrimeCategory = d.Incident != null && d.Incident.IncidentType != null ? d.Incident.IncidentType.TypeName : null,
                    StationName = d.Station != null ? d.Station.StationName : null,
                    StationRegion = d.Station != null ? d.Station.Region : null,
                    StationProvince = d.Station != null ? d.Station.Province : null,
                    CreatedByUserID = d.FK_CreatedByUserID,
                    CreatedByFirstName = d.CreatedByUser != null ? d.CreatedByUser.FirstName : null,
                    CreatedByLastName = d.CreatedByUser != null ? d.CreatedByUser.LastName : null,
                    IncidentID = d.Incident != null ? (int?)d.Incident.IncidentID : null,
                    IncidentOccurrenceDate = d.Incident != null ? (DateTime?)d.Incident.OccurrenceDate : null,
                    IncidentLocationType = d.Incident != null ? d.Incident.LocationType : null,
                    IncidentDescription = d.Incident != null ? d.Incident.Description : null,
                    IncidentType = d.Incident != null && d.Incident.IncidentType != null ? d.Incident.IncidentType.TypeName : null,
                    IncidentAreaName = d.Incident != null && d.Incident.GeoLocation != null ? d.Incident.GeoLocation.AreaName : null,
                    IncidentLatitude = d.Incident != null && d.Incident.GeoLocation != null ? d.Incident.GeoLocation.Latitude : null,
                    IncidentLongitude = d.Incident != null && d.Incident.GeoLocation != null ? d.Incident.GeoLocation.Longitude : null
                })
                .ToListAsync();

            var investigationIds = dockets
                .Where(docket => docket.InvestigationID.HasValue)
                .Select(docket => docket.InvestigationID!.Value)
                .Distinct()
                .ToList();

            var docketIds = dockets
                .Select(docket => docket.DocketID)
                .Distinct()
                .ToList();

            var assignmentRows = investigationIds.Any()
                ? await _db.InvestigationAssignments
                    .AsNoTracking()
                    .Where(assignment => investigationIds.Contains(assignment.FK_InvestigationID))
                    .Select(assignment => new DocketAssignmentRow
                    {
                        InvestigationID = assignment.FK_InvestigationID,
                        DateAssigned = assignment.DateAssigned,
                        AssignmentID = assignment.AssignmentID,
                        DateAccepted = assignment.DateAccepted,
                        InspectionDueAt = assignment.InspectionDueAt,
                        InspectionCompletedAt = assignment.InspectionCompletedAt,
                        AllocationReviewStatus = assignment.AllocationReviewStatus,
                        AllocationReviewNotes = assignment.AllocationReviewNotes,
                        AssignedOfficerUserID = assignment.FK_UserID,
                        UserFirstName = assignment.User != null ? assignment.User.FirstName : null,
                        UserLastName = assignment.User != null ? assignment.User.LastName : null
                    })
                    .ToListAsync()
                : new List<DocketAssignmentRow>();

            var latestAssignmentsByInvestigation = assignmentRows
                .GroupBy(assignment => assignment.InvestigationID)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(assignment => assignment.DateAssigned)
                        .ThenByDescending(assignment => assignment.AssignmentID)
                        .First());

            var suspectRows = docketIds.Any()
                ? await _db.DocketPersonLinks
                    .AsNoTracking()
                    .Where(link => docketIds.Contains(link.FK_DocketID) && link.Person != null && !link.Person.IsArchived)
                    .Select(link => new DocketSuspectRow
                    {
                        DocketID = link.FK_DocketID,
                        RelationshipRole = link.RelationshipRole,
                        PersonRoleCode = link.Person!.PersonRole != null ? link.Person.PersonRole.Code : null,
                        PersonRoleName = link.Person!.PersonRole != null ? link.Person.PersonRole.RoleName : null,
                        FirstName = link.Person!.FirstName,
                        LastName = link.Person!.LastName
                    })
                    .ToListAsync()
                : new List<DocketSuspectRow>();

            var suspectByDocket = suspectRows
                .GroupBy(row => row.DocketID)
                .ToDictionary(
                    group => group.Key,
                    group => group.FirstOrDefault(row =>
                        string.Equals(row.RelationshipRole, "Suspect", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(row.PersonRoleCode, "SUS", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(row.PersonRoleName, "Suspect", StringComparison.OrdinalIgnoreCase)));

            var activePrivacyFlagDocketIds = docketIds.Any()
                ? (await _db.DocketPrivacyFlags
                    .AsNoTracking()
                    .Where(flag => docketIds.Contains(flag.FK_DocketID) && flag.RemovedAt == null)
                    .Select(flag => flag.FK_DocketID)
                    .Distinct()
                    .ToListAsync())
                    .ToHashSet()
                : new HashSet<int>();

            static string FormatName(string? firstName, string? lastName)
            {
                return string.Join(" ", new[] { firstName, lastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            }

            static string FormatJurisdiction(string? region, string? province)
            {
                return string.Join(", ", new[] { region, province }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            }

            var response = dockets.Select(docket =>
            {
                latestAssignmentsByInvestigation.TryGetValue(docket.InvestigationID ?? 0, out var latestAssignment);
                if (string.Equals(latestAssignment?.AllocationReviewStatus, "Unallocated", StringComparison.OrdinalIgnoreCase))
                    latestAssignment = null;
                suspectByDocket.TryGetValue(docket.DocketID, out var suspect);

                return new DocketResponseDto
                {
                    DocketID = docket.DocketID,
                    InvestigationID = docket.InvestigationID,
                    DocketNumber = docket.DocketNumber ?? string.Empty,
                    OBReferenceNumber = docket.OBReferenceNumber ?? string.Empty,
                    FICReferenceNumber = docket.FICReferenceNumber ?? string.Empty,
                    DocketSummary = docket.DocketSummary ?? string.Empty,
                    ModusOperandi = docket.ModusOperandi ?? string.Empty,
                    PriorityLevel = docket.PriorityLevel ?? string.Empty,
                    SensitivityLevel = docket.SensitivityLevel ?? "NORMAL",
                    CaseType = docket.CaseType ?? string.Empty,
                    ComplainantFullName = docket.ComplainantFullName ?? string.Empty,
                    ComplainantContactNumber = docket.ComplainantContactNumber ?? string.Empty,
                    Status = docket.Status ?? string.Empty,
                    DateCreated = docket.DateCreated,
                    DateAllocated = docket.DateAllocated,
                    AssignmentAcceptedAt = latestAssignment?.DateAccepted,
                    InspectionDueAt = latestAssignment?.InspectionDueAt,
                    InspectionCompletedAt = latestAssignment?.InspectionCompletedAt,
                    AllocationReviewStatus = latestAssignment?.AllocationReviewStatus ?? string.Empty,
                    AllocationReviewNotes = latestAssignment?.AllocationReviewNotes ?? string.Empty,
                    DocketArchivedAt = docket.DocketArchivedAt,
                    DocketClosedAt = docket.DocketClosedAt,
                    ReopenedAt = docket.ReopenedAt,
                    CrimeCategory = docket.CrimeCategory ?? string.Empty,
                    StationName = docket.StationName ?? string.Empty,
                    Jurisdiction = FormatJurisdiction(docket.StationRegion, docket.StationProvince),
                    SuspectName = FormatName(suspect?.FirstName, suspect?.LastName),
                    PrivacyFlag = activePrivacyFlagDocketIds.Contains(docket.DocketID),
                    AssignedOfficerUserID = latestAssignment?.AssignedOfficerUserID,
                    AssignedOfficerName = FormatName(latestAssignment?.UserFirstName, latestAssignment?.UserLastName),
                    CreatedByUserID = docket.CreatedByUserID,
                    Incident = docket.IncidentID.HasValue && docket.IncidentOccurrenceDate.HasValue
                        ? new IncidentResponseDto
                        {
                            IncidentID = docket.IncidentID.Value,
                            OccurrenceDate = docket.IncidentOccurrenceDate.Value,
                            LocationType = docket.IncidentLocationType ?? string.Empty,
                            Description = docket.IncidentDescription ?? string.Empty,
                            IncidentType = docket.IncidentType ?? string.Empty,
                            AreaName = docket.IncidentAreaName ?? string.Empty,
                            Latitude = docket.IncidentLatitude ?? string.Empty,
                            Longitude = docket.IncidentLongitude ?? string.Empty
                        }
                        : null,
                    CreatedByName = FormatName(docket.CreatedByFirstName, docket.CreatedByLastName)
                };
            }).ToList();

            return ServiceResult<List<DocketResponseDto>>.SuccessResult(response);
        }

        // UC 3.3 - Update Docket
        public async Task<ServiceResult<bool>> UpdateDocketAsync(int docketId, UpdateDocketDto updateDto, int requestingUserId)
        {
            var requestingUser = await _userRepository.GetByIdAsync(requestingUserId);
            if (requestingUser == null)
                return ServiceResult<bool>.Failure("User not found.");

            var docket = await _docketRepository.GetByIdAsync(docketId);
            if (docket == null)
                return ServiceResult<bool>.Failure("Docket not found.");

            if (docket.DocketStatus?.StatusName is "CLOSED" or "ARCHIVED")
                return ServiceResult<bool>.Failure("Closed or archived dockets are read-only. Re-open the docket before editing it.");

            var isCreator = docket.FK_CreatedByUserID == requestingUserId;
            var isDocketAdmin = requestingUser.Role?.Name == "Docket Administrator";
            var isSystemAdmin = requestingUser.Role?.Name == "System Administrator";

            if (!isCreator && !isDocketAdmin && !isSystemAdmin)
                return ServiceResult<bool>.Failure("You do not have permission to update this docket.");

            // Business rule: Reason must be at least 20 characters
            if (string.IsNullOrWhiteSpace(updateDto.ReasonForUpdate) || updateDto.ReasonForUpdate.Length < 20)
                return ServiceResult<bool>.Failure("A reason of at least 20 characters is required for all docket edits.");

            var sensitivityLevel = string.IsNullOrWhiteSpace(updateDto.SensitivityLevel)
                ? docket.SensitivityLevel ?? "NORMAL"
                : updateDto.SensitivityLevel.Trim().ToUpperInvariant();
            if (sensitivityLevel is not ("NORMAL" or "RESTRICTED" or "HIGHLY_SENSITIVE"))
                return ServiceResult<bool>.Failure("Sensitivity level must be Normal, Restricted or Highly Sensitive.");

            var oldValues = new { docket.DocketSummary, docket.ModusOperandi, docket.SensitivityLevel };

            docket.DocketSummary = updateDto.DocketSummary ?? docket.DocketSummary;
            docket.ModusOperandi = updateDto.ModusOperandi ?? docket.ModusOperandi;
            docket.SensitivityLevel = sensitivityLevel;

            await _docketRepository.UpdateAsync(docket);

            var newValues = new { docket.DocketSummary, docket.ModusOperandi, docket.SensitivityLevel };

            await _auditService.LogActionAsync("UPDATE_DOCKET", "Docket", docketId, oldValues, newValues, requestingUserId, updateDto.ReasonForUpdate);

            return ServiceResult<bool>.SuccessResult(true, "Docket updated successfully.");
        }

        // UC 3.5 - Allocate Docket to Investigating Officer
        public async Task<ServiceResult<bool>> AllocateDocketAsync(int docketId, AllocateDocketDto allocateDto, int allocatedByUserId)
        {
            var allocatedBy = await _userRepository.GetByIdAsync(allocatedByUserId);
            if (allocatedBy == null)
                return ServiceResult<bool>.Failure("User not found.");

            var isSystemAdmin = allocatedBy.Role?.Name == "System Administrator";
            var isAllocator = allocatedBy.Role?.Name == "DSC Supervisor" || isSystemAdmin;
            if (!isAllocator)
                return ServiceResult<bool>.Failure("Only DSC Supervisors may allocate dockets.");

            var docket = await _docketRepository.GetByIdAsync(docketId);
            if (docket == null)
                return ServiceResult<bool>.Failure("Docket not found.");

            if (!isSystemAdmin && docket.FK_StationID != allocatedBy.FK_StationID)
                return ServiceResult<bool>.Failure("Docket must belong to your station.");

            // Only OPEN dockets can be allocated
            if (docket.DocketStatus?.StatusName != "OPEN")
                return ServiceResult<bool>.Failure($"This docket is no longer in OPEN status.");

            if (!docket.FK_InvestigationID.HasValue)
                return ServiceResult<bool>.Failure("This docket is missing its investigation record and cannot be allocated. Contact a system administrator.");

            var latestAssignmentStatus = await _db.InvestigationAssignments
                .AsNoTracking()
                .Where(assignment => assignment.FK_InvestigationID == docket.FK_InvestigationID.Value)
                .OrderByDescending(assignment => assignment.DateAssigned)
                .ThenByDescending(assignment => assignment.AssignmentID)
                .Select(assignment => assignment.AllocationReviewStatus)
                .FirstOrDefaultAsync();
            if (string.Equals(latestAssignmentStatus, "Officer Rejected", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(latestAssignmentStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<bool>.Failure("A rejected docket assignment cannot be reallocated.");

            var investigatingOfficer = await _userRepository.GetByIdAsync(allocateDto.InvestigatingOfficerUserID);
            if (investigatingOfficer == null)
                return ServiceResult<bool>.Failure("Investigating Officer not found.");

            if (investigatingOfficer.Role?.Name != "Investigating Officer" ||
                !investigatingOfficer.IsActive || !investigatingOfficer.IsActivated || investigatingOfficer.IsLocked ||
                !string.Equals(investigatingOfficer.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<bool>.Failure("The selected user must be an active, activated and unlocked Investigating Officer.");

            // Must be from same station
            if (investigatingOfficer.FK_StationID != allocatedBy.FK_StationID &&
                !isSystemAdmin)
                return ServiceResult<bool>.Failure("Investigating Officer must be from the same station.");

            var maxActiveDockets = await GetMaxActiveDocketsPerOfficerAsync();
            var activeDocketCount = await GetOfficerActiveDocketCountAsync(investigatingOfficer.UserID);
            if (activeDocketCount >= maxActiveDockets)
            {
                var officerName = $"{investigatingOfficer.FirstName} {investigatingOfficer.LastName}".Trim();
                return ServiceResult<bool>.Failure(
                    $"{officerName} cannot be allocated this docket because they already manage {activeDocketCount} active dockets, which is the configured maximum of {maxActiveDockets}.");
            }

            // Create Investigation Assignment
            var assignment = new InvestigationAssignment
            {
                Role = "PRIMARY_IO",
                DateAssigned = DateTime.UtcNow,
                FK_InvestigationID = docket.FK_InvestigationID.Value,
                FK_UserID = allocateDto.InvestigatingOfficerUserID,
                FK_AssignedByUserID = allocatedByUserId,
                // The assigned Investigating Officer, not a DSC Supervisor, accepts or rejects an allocation.
                AllocationReviewStatus = "Awaiting Officer Response"
            };
            await _docketRepository.CreateAssignmentAsync(assignment);

            // Update docket status
            docket.DateAllocated = DateTime.UtcNow;
            docket.FK_DocketStatusID = 2; // ALLOCATED
            await _docketRepository.UpdateAsync(docket);

            // Create notification
            var notification = new Notification
            {
                Title = "New Docket Allocated",
                Message = $"You have been allocated docket {docket.DocketNumber}. Please log in to review and accept.",
                PriorityLevel = "HIGH",
                FK_UserID = allocateDto.InvestigatingOfficerUserID,
                IsEmailSent = false,
                IsSMSSent = false
            };
            await _notificationRepository.CreateAsync(notification);

            // Send email notification
            await _emailService.SendNotificationEmailAsync(investigatingOfficer.EmailAddress, "New Docket Allocated",
                $"You have been allocated docket {docket.DocketNumber} (OB: {docket.OBReferenceNumber}). Please log in to review within 24 hours.");

            await _smsService.SendSmsAsync(
                investigatingOfficer.UserID,
                investigatingOfficer.PhoneNumber,
                $"SAPS DDMS: docket {docket.DocketNumber} has been allocated to you. Log in to review and accept the assignment.",
                "Docket allocation");

            await _auditService.LogActionAsync("ALLOCATE_DOCKET", "Docket", docketId, null,
                new { InvestigatingOfficer = allocateDto.InvestigatingOfficerUserID }, allocatedByUserId);

            return ServiceResult<bool>.SuccessResult(true, $"Docket allocated to {investigatingOfficer.FirstName} {investigatingOfficer.LastName}.");
        }

        public async Task<ServiceResult<List<UserDto>>> GetAvailableInvestigatingOfficersAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return ServiceResult<List<UserDto>>.Failure("User not found.");

            var officers = await _docketRepository.GetAvailableInvestigatingOfficersAsync(user.FK_StationID);
            var maxActiveDockets = await GetMaxActiveDocketsPerOfficerAsync();
            var workloadByOfficer = await GetOfficerActiveDocketCountsAsync(officers.Select(officer => officer.UserID));

            var response = officers.Select(o => new UserDto
            {
                Id = o.UserID,
                FirstName = o.FirstName,
                LastName = o.LastName,
                FullName = $"{o.FirstName} {o.LastName}",
                PersonnelNumber = o.Username,
                Rank = o.Rank,
                ActiveDocketCount = workloadByOfficer.GetValueOrDefault(o.UserID),
                MaximumActiveDockets = maxActiveDockets,
                AtCapacity = workloadByOfficer.GetValueOrDefault(o.UserID) >= maxActiveDockets
            })
            .OrderBy(officer => officer.AtCapacity)
            .ThenBy(officer => officer.ActiveDocketCount)
            .ThenBy(officer => officer.FullName)
            .ToList();

            return ServiceResult<List<UserDto>>.SuccessResult(response);
        }
    }
}
