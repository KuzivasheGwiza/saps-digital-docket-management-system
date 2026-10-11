using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using SAPS.DDMS.API.Controllers;
using SAPS.DDMS.API.Security;

namespace SAPS.DDMS.API.Tests.UseCaseTests;

public class AuthorizationContractTests
{
    [Fact]
    public void PermissionProtectedControllers_UseExpectedPolicies()
    {
        AssertClassPolicy<DocketController>(PermissionCatalog.DocketsView);
        AssertClassPolicy<ReportsController>(PermissionCatalog.ReportsView);
        AssertClassPolicy<BackupController>(PermissionCatalog.BackupManage);
        AssertClassPolicy<AuditController>(PermissionCatalog.AuditView);
        AssertClassPolicy<DataExchangeController>(PermissionCatalog.DataExchangeManage);
        AssertClassPolicy<SmsQueueController>(PermissionCatalog.SmsManage);

        AssertMethodPolicy<UserController>(nameof(UserController.GetAllUsers), PermissionCatalog.UsersManage);
        AssertMethodPolicy<UserController>(nameof(UserController.GetUserById), PermissionCatalog.UsersManage);
        AssertMethodPolicy<UserController>(nameof(UserController.CreateUser), PermissionCatalog.UsersManage);
        AssertMethodPolicy<UserController>(nameof(UserController.ResendActivationEmail), PermissionCatalog.UsersManage);
        AssertMethodPolicy<UserController>(nameof(UserController.UpdateUser), PermissionCatalog.UsersManage);
        AssertMethodPolicy<UserController>(nameof(UserController.DeactivateUser), PermissionCatalog.UsersManage);
        AssertMethodPolicy<UserController>(nameof(UserController.SuspendUser), PermissionCatalog.UsersManage);
        AssertMethodPolicy<UserController>(nameof(UserController.ReactivateUser), PermissionCatalog.UsersManage);
        AssertMethodPolicy<UserController>(nameof(UserController.UnlockUser), PermissionCatalog.UsersManage);
        AssertMethodPolicy<UserController>(nameof(UserController.GetRoleHistory), PermissionCatalog.UsersManage);

        AssertMethodPolicy<AiController>(nameof(AiController.GetAiLogs), PermissionCatalog.AiLogView);
    }

    [Fact]
    public void DocketAndBundleActions_RequireExpectedRoles()
    {
        AssertMethodRoles<DocketController>(nameof(DocketController.CreateDocket), "CSC Officer");
        AssertMethodRoles<DocketController>(nameof(DocketController.UpdateDocket), "CSC Officer", "Docket Administrator");
        AssertMethodRoles<DocketController>(nameof(DocketController.AllocateDocket), "DSC Supervisor");
        AssertMethodRoles<DocketController>(nameof(DocketController.GetAvailableInvestigatingOfficers), "DSC Supervisor");

        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.AcceptAssignment), "Investigating Officer");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.ReviewAllocation), "DSC Supervisor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.CompleteInspection), "DSC Supervisor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.ChangeStatus), "Investigating Officer", "DSC Supervisor", "Docket Administrator");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.ProsecutorPreview), "DSC Supervisor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.CreateProsecutorBundle), "Investigating Officer");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.GetProsecutorBundlesForDocket), "Investigating Officer", "DSC Supervisor", "Prosecutor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.GetProsecutorBundle), "Investigating Officer", "DSC Supervisor", "Prosecutor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.UpdateBundle), "Investigating Officer");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.GetEligibleProsecutors), "DSC Supervisor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.PreviewBundleShare), "DSC Supervisor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.ShareBundle), "DSC Supervisor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.ExportBundle), "DSC Supervisor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.DownloadBundle), "DSC Supervisor", "Prosecutor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.SetBundleItemPrivacyFlag), "Investigating Officer", "DSC Supervisor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.GetBundleItemPrivacyFlagHistory), "DSC Supervisor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.GetPrivacyFlagHistory), "DSC Supervisor", "Docket Administrator");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.ArchiveDocket), "Docket Administrator");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.RecordCourtOutcome), "DSC Supervisor");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.CloseCase), "DSC Supervisor", "Docket Administrator");
        AssertMethodRoles<DocketActionsController>(nameof(DocketActionsController.ReopenCase), "DSC Supervisor", "Docket Administrator");
    }

    [Fact]
    public void AiPersonEvidenceAndNoteActions_RequireExpectedRoles()
    {
        AssertMethodRoles<AiController>("GenerateSummary", "Investigating Officer", "DSC Supervisor", "Docket Administrator");
        AssertMethodRoles<AiController>("ConfirmSummary", "Investigating Officer", "DSC Supervisor", "Docket Administrator");
        AssertMethodRoles<AiController>("ExtractDocketData", "Investigating Officer", "Docket Administrator");
        AssertMethodRoles<AiController>("VerifyExtractions", "Investigating Officer", "Docket Administrator");
        AssertMethodRoles<AiController>("SuggestRelatedCases", "Investigating Officer", "DSC Supervisor", "Docket Administrator");
        AssertMethodRoles<AiController>("ConfirmRelatedCases", "Investigating Officer", "DSC Supervisor", "Docket Administrator");

        AssertMethodRoles<PersonController>(nameof(PersonController.CreatePerson), "System Administrator", "CSC Officer", "Investigating Officer", "DSC Supervisor", "Docket Administrator");
        AssertMethodRoles<PersonController>(nameof(PersonController.UpdatePerson), "System Administrator", "CSC Officer", "Investigating Officer", "DSC Supervisor", "Docket Administrator");
        AssertMethodRoles<PersonController>(nameof(PersonController.LinkPersonToDocket), "System Administrator", "CSC Officer", "Investigating Officer", "DSC Supervisor");
        AssertMethodRoles<PersonController>(nameof(PersonController.ArchivePerson), "System Administrator", "Investigating Officer", "DSC Supervisor", "Docket Administrator");

        AssertClassRoles<EvidenceController>("Investigating Officer", "DSC Supervisor", "Records Clerk");
        AssertMethodRoles<EvidenceController>(nameof(EvidenceController.CreateEvidence), "Investigating Officer");
        AssertMethodRoles<EvidenceController>(nameof(EvidenceController.UpdateEvidence), "Investigating Officer");
        AssertMethodRoles<FilesController>(nameof(FilesController.GetEvidenceImage), "Investigating Officer", "DSC Supervisor", "Records Clerk");
        AssertMethodRoles<EvidenceTransfersController>(nameof(EvidenceTransfersController.TransferEvidence), "Records Clerk", "Investigating Officer");
        AssertMethodRoles<EvidenceTransfersController>(nameof(EvidenceTransfersController.GetChainOfCustody), "Records Clerk", "Investigating Officer", "DSC Supervisor");

        AssertClassRoles<InvestigationController>("Investigating Officer", "DSC Supervisor", "Docket Administrator");
        AssertMethodRoles<InvestigationController>(nameof(InvestigationController.CreateInvestigationNote), "Investigating Officer");
        AssertMethodRoles<InvestigationController>(nameof(InvestigationController.UpdateInvestigationNote), "Investigating Officer", "Docket Administrator");
        AssertMethodRoles<InvestigationController>(nameof(InvestigationController.GetNoteVersions), "Investigating Officer", "Docket Administrator", "DSC Supervisor");
        AssertMethodRoles<InvestigationController>(nameof(InvestigationController.RequestArchiveNote), "Investigating Officer");
        AssertMethodRoles<InvestigationController>(nameof(InvestigationController.GetArchiveRequests), "DSC Supervisor");
        AssertMethodRoles<InvestigationController>(nameof(InvestigationController.ReviewArchiveRequest), "DSC Supervisor");
    }

    [Fact]
    public void ReportRoleMap_StaysAlignedToExpectedConsumers()
    {
        var field = typeof(ReportsController).GetField("ReportRolesByKey", BindingFlags.NonPublic | BindingFlags.Static);
        var actual = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(field?.GetValue(null));

        Assert.Equal(new[] { "DSC Supervisor" }, actual["users-list"]);
        Assert.Equal(new[] { "DSC Supervisor", "Docket Administrator" }, actual["dockets-list"]);
        Assert.All(actual.Where(item => item.Key != "dockets-list"), item => Assert.Equal(new[] { "DSC Supervisor" }, item.Value));
    }

    private static void AssertClassPolicy<TController>(string expectedPolicy) where TController : class
    {
        var attribute = typeof(TController).GetCustomAttributes<AuthorizeAttribute>(true)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Policy));

        Assert.NotNull(attribute);
        Assert.Equal(expectedPolicy, attribute!.Policy);
    }

    private static void AssertClassRoles<TController>(params string[] expectedRoles) where TController : class
    {
        var attribute = typeof(TController).GetCustomAttributes<AuthorizeAttribute>(true)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Roles));

        Assert.NotNull(attribute);
        Assert.Equal(expectedRoles, SplitRoles(attribute!.Roles));
    }

    private static void AssertMethodPolicy<TController>(string methodName, string expectedPolicy) where TController : class
    {
        var attribute = GetMethodAuthorizeAttributes(typeof(TController), methodName)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Policy));

        Assert.NotNull(attribute);
        Assert.Equal(expectedPolicy, attribute!.Policy);
    }

    private static void AssertMethodRoles<TController>(string methodName, params string[] expectedRoles) where TController : class
    {
        var attribute = GetMethodAuthorizeAttributes(typeof(TController), methodName)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Roles));

        Assert.NotNull(attribute);
        Assert.Equal(expectedRoles, SplitRoles(attribute!.Roles));
    }

    private static AuthorizeAttribute[] GetMethodAuthorizeAttributes(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.GetCustomAttributes<AuthorizeAttribute>(true).ToArray();
    }

    private static string[] SplitRoles(string? roles) =>
        (roles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
