using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Repositories;
using SAPS.DDMS.API.Services;

namespace SAPS.DDMS.API.Tests.Support;

internal sealed class FakeAuditService : IAuditService
{
    public readonly List<(string ActionType, string Entity, int? EntityId, string? Reason)> Entries = [];

    public Task LogActionAsync(string actionType, string affectedEntity, int? affectedEntityId, object oldValue, object newValue, int userId, string reason = null, string ipAddress = null)
    {
        Entries.Add((actionType, affectedEntity, affectedEntityId, reason));
        return Task.CompletedTask;
    }
}

internal sealed class FakeEmailService : IEmailService
{
    public string? LastPasswordResetOtp { get; private set; }
    public string? LastPasswordResetRecipient { get; private set; }
    public int? LastPasswordResetExpiryMinutes { get; private set; }

    public Task<bool> SendActivationEmailAsync(string email, string fullName, string activationLink) => Task.FromResult(true);
    public Task<bool> SendNotificationEmailAsync(string email, string subject, string message) => Task.FromResult(true);
    public Task<bool> SendPasswordResetEmailAsync(string email, string resetLink) => Task.FromResult(true);
    public Task<bool> SendPasswordResetOtpEmailAsync(string email, string fullName, string otpCode, int expiryMinutes)
    {
        LastPasswordResetRecipient = email;
        LastPasswordResetOtp = otpCode;
        LastPasswordResetExpiryMinutes = expiryMinutes;
        return Task.FromResult(true);
    }
}

internal sealed class FakeSmsService : ISmsService
{
    public Task<bool> SendSmsAsync(int? userId, string? recipientNumber, string message, string purpose) => Task.FromResult(true);
    public Task<bool> SendOtpAsync(User user, string otpCode) => Task.FromResult(true);
}

internal sealed class FakeSystemConfigService : ISystemConfigService
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public FakeSystemConfigService(params (string Key, string Value)[] values)
    {
        foreach (var (key, value) in values)
            _values[key] = value;
    }

    public Task<int> GetIntAsync(string key, int defaultValue) =>
        Task.FromResult(_values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : defaultValue);

    public Task<bool> GetBoolAsync(string key, bool defaultValue) =>
        Task.FromResult(_values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : defaultValue);

    public Task<string> GetStringAsync(string key, string defaultValue) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : defaultValue);
}

internal sealed class FakeUserRepository : IUserRepository
{
    public User? UserById { get; set; }
    public User? UserByEmail { get; set; }
    public User? UserByResetToken { get; set; }
    public User? UserByUsername { get; set; }
    public Session? ActiveSession { get; set; }
    public string? LastUsernameLookup { get; private set; }
    public string? LastEmailLookup { get; private set; }

    public Task<User?> GetByUsernameAsync(string username)
    {
        LastUsernameLookup = username;
        return Task.FromResult(UserByUsername);
    }
    public Task<User?> GetByEmailAsync(string email)
    {
        LastEmailLookup = email;
        return Task.FromResult(UserByEmail);
    }
    public Task<User?> GetByIdAsync(int id) => Task.FromResult(UserById);
    public Task<User?> GetByResetTokenAsync(string tokenHash) => Task.FromResult(UserByResetToken);
    public Task<User?> GetByActivationTokenAsync(string tokenHash) => Task.FromResult<User?>(null);
    public Task<List<User>> GetAllAsync(int? roleId = null, bool? isActive = null) => Task.FromResult(new List<User>());
    public Task<User> CreateAsync(User user) => Task.FromResult(user);
    public Task UpdateAsync(User user)
    {
        UserById = user;
        UserByEmail = user;
        UserByResetToken = user;
        UserByUsername = user;
        return Task.CompletedTask;
    }
    public Task<bool> UsernameExistsAsync(string username) => Task.FromResult(false);
    public Task<bool> EmailExistsAsync(string email) => Task.FromResult(false);
    public Task<List<Role>> GetRolesAsync() => Task.FromResult(new List<Role>());
    public Task<List<Station>> GetStationsAsync() => Task.FromResult(new List<Station>());
    public Task<List<User>> GetUsersByRoleAndStationAsync(string roleName, int stationId) => Task.FromResult(new List<User>());
    public Task<Session> CreateSessionAsync(Session session) => Task.FromResult(session);
    public Task<Session?> GetActiveSessionAsync(int userId) => Task.FromResult(ActiveSession);
    public Task UpdateSessionAsync(Session session)
    {
        ActiveSession = session;
        return Task.CompletedTask;
    }
}

internal sealed class FakePersonRepository : IPersonRepository
{
    public Person? PersonById { get; set; }
    public bool IdNumberExists { get; set; }
    public List<DocketPersonLink> Links { get; set; } = [];
    public List<PersonRole> Roles { get; set; } = [];

    public Task<Person?> GetByIdAsync(int id) => Task.FromResult(PersonById);
    public Task<Person?> GetByIdNumberAsync(string idNumber) => Task.FromResult<Person?>(null);
    public Task<List<Person>> SearchAsync(string searchTerm, int stationId) => Task.FromResult(new List<Person>());
    public Task<Person> CreateAsync(Person person)
    {
        person.PersonID = person.PersonID == 0 ? 101 : person.PersonID;
        return Task.FromResult(person);
    }
    public Task UpdateAsync(Person person) => Task.CompletedTask;
    public Task<bool> IdNumberExistsAsync(string idNumber) => Task.FromResult(IdNumberExists);
    public Task<DocketPersonLink> LinkPersonToDocketAsync(DocketPersonLink link)
    {
        link.LinkID = link.LinkID == 0 ? 12 : link.LinkID;
        Links.Add(link);
        return Task.FromResult(link);
    }
    public Task<List<DocketPersonLink>> GetDocketLinksForPersonAsync(int personId) => Task.FromResult(Links);
    public Task<bool> IsPersonLinkedToDocketAsync(int personId, int docketId) => Task.FromResult(false);
    public Task<List<PersonRole>> GetPersonRolesAsync() => Task.FromResult(Roles);
}

internal sealed class FakeDocketRepository : IDocketRepository
{
    public Docket? DocketById { get; set; }

    public Task<Docket?> GetByIdAsync(int id) => Task.FromResult(DocketById);
    public Task<Docket?> GetByIdWithDetailsAsync(int id) => Task.FromResult(DocketById);
    public Task<Docket?> GetByDocketNumberAsync(string docketNumber) => Task.FromResult<Docket?>(null);
    public Task<Docket?> GetByOBReferenceAsync(string obReference) => Task.FromResult<Docket?>(null);
    public Task<List<Docket>> GetAllWithDetailsAsync(string status = null) => Task.FromResult(new List<Docket>());
    public Task<List<Docket>> GetByStationIdAsync(int stationId, string status = null) => Task.FromResult(new List<Docket>());
    public Task<List<Docket>> GetByStationIdWithDetailsAsync(int stationId, string status = null) => Task.FromResult(new List<Docket>());
    public Task<List<Docket>> GetByInvestigatingOfficerAsync(int userId) => Task.FromResult(new List<Docket>());
    public Task<Docket> CreateAsync(Docket docket) => Task.FromResult(docket);
    public Task<Investigation> CreateInvestigationAsync(Investigation investigation) => Task.FromResult(investigation);
    public Task UpdateAsync(Docket docket) => Task.CompletedTask;
    public Task<bool> OBReferenceExistsAsync(string obReference) => Task.FromResult(false);
    public Task<InvestigationAssignment> CreateAssignmentAsync(InvestigationAssignment assignment) => Task.FromResult(assignment);
    public Task<DocketTimer?> GetActiveTimerAsync(int docketId) => Task.FromResult<DocketTimer?>(null);
    public Task<DocketTimer> AddTimerAsync(DocketTimer timer) => Task.FromResult(timer);
    public Task UpdateTimerAsync(DocketTimer timer) => Task.CompletedTask;
    public Task<List<InvestigationAssignment>> GetAssignmentsForDocketAsync(int docketId) => Task.FromResult(new List<InvestigationAssignment>());
    public Task<List<User>> GetAvailableInvestigatingOfficersAsync(int stationId) => Task.FromResult(new List<User>());
}

internal sealed class FakeAiService : IAiService
{
    public Task<string> SummarizeDocketAsync(int docketId, int userId) => Task.FromResult(string.Empty);
    public Task<Dictionary<string, object?>> ExtractDocketDataAsync(int docketId, int userId) => Task.FromResult(new Dictionary<string, object?>());
    public Task<IEnumerable<(int relatedDocketId, double score, string reason)>> SuggestRelatedCasesAsync(int docketId, int maxResults = 5) =>
        Task.FromResult<IEnumerable<(int relatedDocketId, double score, string reason)>>([]);
    public Task<bool> VerifyExtractionsAsync(int docketId, object verifiedValues, int userId) => Task.FromResult(true);
}

internal sealed class FakeReportPdfService : IReportPdfService
{
    public byte[] BuildReportPdf(ReportPdfRequest request) => [];

    public byte[] BuildTextReport(string title, string generatedBy, IEnumerable<string> lines, IEnumerable<ReportPdfChartItem>? chart = null) => [];
}

internal sealed class FakeFileStorageService : IFileStorageService
{
    public Task<string> SaveFileAsync(IFormFile file, string subDirectory) => Task.FromResult(string.Empty);
    public Task<bool> DeleteFileAsync(string filePath) => Task.FromResult(true);
    public string ComputeFileHash(IFormFile file) => string.Empty;
    public Task<byte[]> GetFileAsync(string filePath) => Task.FromResult(Array.Empty<byte>());
}

internal static class TestConfiguration
{
    public static IConfiguration Build() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppUrl"] = "http://localhost:4200",
                ["JwtSettings:SecretKey"] = "12345678901234567890123456789012",
                ["JwtSettings:Issuer"] = "Tests",
                ["JwtSettings:Audience"] = "Tests"
            })
            .Build();
}
