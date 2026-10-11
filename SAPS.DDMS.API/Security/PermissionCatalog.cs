using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;

namespace SAPS.DDMS.API.Security;

public sealed record PermissionDefinition(string Name, string Group, string Description);

public static class PermissionCatalog
{
    public const string DashboardView = "Dashboard.View";
    public const string DocketsView = "Dockets.View";
    public const string PersonsView = "Persons.View";
    public const string EvidenceView = "Evidence.View";
    public const string CalendarView = "Calendar.View";
    public const string ReportsView = "Reports.View";
    public const string StationsView = "Stations.View";
    public const string UsersManage = "Users.Manage";
    public const string RolesManage = "Roles.Manage";
    public const string FaqManage = "Faq.Manage";
    public const string ConfigurationManage = "Configuration.Manage";
    public const string BackupManage = "Backup.Manage";
    public const string AuditView = "Audit.View";
    public const string AiLogView = "AI.Log.View";
    public const string SmsManage = "SMS.Manage";
    public const string DataExchangeManage = "DataExchange.Manage";

    public static readonly IReadOnlyList<PermissionDefinition> All = new[]
    {
        new PermissionDefinition(DashboardView, "Operations", "View operational dashboard metrics and charts."),
        new PermissionDefinition(DocketsView, "Operations", "View docket lists, details, and calendar data."),
        new PermissionDefinition(PersonsView, "Operations", "View person records linked to dockets."),
        new PermissionDefinition(EvidenceView, "Operations", "View evidence records and custody information."),
        new PermissionDefinition(CalendarView, "Operations", "View the docket and inspection calendar."),
        new PermissionDefinition(ReportsView, "Decision support", "View and export authorised management reports."),
        new PermissionDefinition(StationsView, "Reference data", "View the station hierarchy."),
        new PermissionDefinition(UsersManage, "Administration", "Create, update, suspend, and reactivate users."),
        new PermissionDefinition(RolesManage, "Administration", "Create access profiles and change their permissions."),
        new PermissionDefinition(FaqManage, "Administration", "Maintain and publish help FAQs."),
        new PermissionDefinition(ConfigurationManage, "Administration", "Maintain configurable timers and business rules."),
        new PermissionDefinition(BackupManage, "Administration", "Create, download, and review system backups."),
        new PermissionDefinition(AuditView, "Governance", "Search and export the immutable audit trail."),
        new PermissionDefinition(AiLogView, "Governance", "Review AI decision-support interactions."),
        new PermissionDefinition(SmsManage, "Governance", "Review and dispatch operational SMS messages."),
        new PermissionDefinition(DataExchangeManage, "Data exchange", "Export Excel/JSON data and import validated lookup JSON.")
    };

    public static IReadOnlyCollection<string> DefaultsForRole(string? roleName)
    {
        var common = new[] { DashboardView, DocketsView, StationsView };
        return roleName switch
        {
            "System Administrator" => All
                .Where(item => item.Name != ReportsView &&
                               item.Name != DocketsView &&
                               item.Name != CalendarView)
                .Select(item => item.Name)
                .ToArray(),
            "DSC Supervisor" => common.Concat(new[] { PersonsView, EvidenceView, CalendarView, ReportsView, AiLogView, SmsManage }).ToArray(),
            "Investigating Officer" => common.Concat(new[] { PersonsView, EvidenceView, CalendarView }).ToArray(),
            "CSC Officer" => common.Concat(new[] { PersonsView, CalendarView }).ToArray(),
            "Docket Administrator" => common.Concat(new[] { PersonsView, CalendarView, ReportsView }).ToArray(),
            "Records Clerk" => common.Concat(new[] { EvidenceView, CalendarView }).ToArray(),
            "Prosecutor" => common,
            _ => Array.Empty<string>()
        };
    }

    public static async Task<IReadOnlyCollection<string>> ResolveForRoleAsync(
        ApplicationDbContext db,
        int roleId,
        string? roleName,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(roleName, "System Administrator", StringComparison.OrdinalIgnoreCase))
            return DefaultsForRole(roleName);

        var role = await db.Roles.AsNoTracking()
            .Where(item => item.RoleID == roleId)
            .Select(item => new { item.Permissions })
            .SingleOrDefaultAsync(cancellationToken);

        // A null value identifies an original seeded role that has not yet been
        // customised. Once an administrator saves a profile, even an empty
        // permission set is explicit and therefore disables all module access.
        if (role?.Permissions is null)
            return DefaultsForRole(roleName);

        var assigned = await db.RolePermissions.AsNoTracking()
            .Where(item => item.RoleID == roleId && item.AccessLevel == "Allow")
            .Select(item => item.Permission!.Name!)
            .Where(name => name != null)
            .ToListAsync(cancellationToken);

        return assigned
            .Where(name => All.Any(definition => definition.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionAuthorizationHandler(IServiceScopeFactory scopeFactory)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdValue = context.User.FindFirstValue("userId")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!int.TryParse(userIdValue, out var userId)) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking()
            .Where(item => item.UserID == userId && item.IsActive && !item.IsLocked)
            .Select(item => new { item.FK_RoleID, RoleName = item.Role!.Name })
            .SingleOrDefaultAsync();
        if (user == null) return;

        var permissions = await PermissionCatalog.ResolveForRoleAsync(db, user.FK_RoleID, user.RoleName);
        if (permissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase) ||
            requirement.Permission.Equals(PermissionCatalog.DocketsView, StringComparison.OrdinalIgnoreCase) &&
            permissions.Contains(PermissionCatalog.CalendarView, StringComparer.OrdinalIgnoreCase))
            context.Succeed(requirement);
    }
}
