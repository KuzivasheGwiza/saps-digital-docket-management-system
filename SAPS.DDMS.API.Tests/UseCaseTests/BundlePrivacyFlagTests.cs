using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Controllers;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Tests.Support;

namespace SAPS.DDMS.API.Tests.UseCaseTests;

public class BundlePrivacyFlagTests
{
    [Fact]
    public async Task SetBundleItemPrivacyFlag_RejectsDuplicateActiveFlag_ForInvestigatingOfficer()
    {
        var db = BuildDb(nameof(SetBundleItemPrivacyFlag_RejectsDuplicateActiveFlag_ForInvestigatingOfficer));
        SeedBundleAccessGraph(db, actingRole: "Investigating Officer", actingUserId: 3, isPrivate: true);

        var controller = BuildController(db, 3);

        var result = await controller.SetBundleItemPrivacyFlag(10, 501, new DocketActionsController.PrivacyFlagDto
        {
            IsPrivate = true,
            Reason = "Sensitive witness details"
        });

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("already has an active privacy flag", conflict.Value?.ToString());
    }

    [Fact]
    public async Task GetBundleItemPrivacyFlagHistory_ReturnsChronologicalAuditTrail()
    {
        var db = BuildDb(nameof(GetBundleItemPrivacyFlagHistory_ReturnsChronologicalAuditTrail));
        SeedBundleAccessGraph(db, actingRole: "DSC Supervisor", actingUserId: 2, isPrivate: true);

        db.UserAuditLogs.AddRange(
            new UserAuditLog
            {
                UserAuditLogID = 1,
                ActionType = "CREATE_PRIVACY_FLAG",
                AffectedEntity = "ProsecutorBundleItem",
                AffectedEntityID = 501,
                FK_UserID = 2,
                Timestamp = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
                Reason = "Initial flag"
            },
            new UserAuditLog
            {
                UserAuditLogID = 2,
                ActionType = "REMOVE_PRIVACY_FLAG",
                AffectedEntity = "ProsecutorBundleItem",
                AffectedEntityID = 501,
                FK_UserID = 2,
                Timestamp = new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc),
                Reason = "Reviewed and cleared"
            });
        await db.SaveChangesAsync();

        var controller = BuildController(db, 2);

        var result = await controller.GetBundleItemPrivacyFlagHistory(501);

        var ok = Assert.IsType<OkObjectResult>(result);
        var history = ok.Value!;
        var historyProperty = history.GetType().GetProperty("History");
        var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(historyProperty!.GetValue(history));
        var list = rows.Cast<object>().ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal("Created", list[0].GetType().GetProperty("Action")!.GetValue(list[0])?.ToString());
        Assert.Equal("Removed", list[1].GetType().GetProperty("Action")!.GetValue(list[1])?.ToString());
    }

    private static ApplicationDbContext BuildDb(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedBundleAccessGraph(ApplicationDbContext db, string actingRole, int actingUserId, bool isPrivate)
    {
        var role = new Role { RoleID = 1, Name = actingRole };
        var station = new Station { StationID = 1, StationName = "Pretoria Central" };
        var investigationStatus = new InvestigationStatus { InvestigationStatusID = 1, StatusName = "Active" };
        var docketStatus = new DocketStatus { DocketStatusID = 1, StatusName = "Open" };
        var actingUser = new User
        {
            UserID = actingUserId,
            Username = $"user{actingUserId}",
            PersonnelNumber = $"PN{actingUserId:000000}",
            FirstName = "Test",
            LastName = "User",
            FK_RoleID = 1,
            Role = role,
            FK_StationID = 1,
            Station = station,
            IsActive = true,
            IsActivated = true,
            AccountStatus = "Active",
            IsLocked = false,
            EmailAddress = $"user{actingUserId}@test.local",
            PasswordHash = "hash"
        };

        var investigation = new Investigation
        {
            InvestigationID = 200,
            Title = "Privacy flag investigation",
            FK_InvestigationStatusID = 1,
            InvestigationStatus = investigationStatus
        };
        var assignment = new InvestigationAssignment
        {
            AssignmentID = 300,
            FK_InvestigationID = 200,
            Investigation = investigation,
            FK_UserID = actingUserId,
            User = actingUser,
            DateAssigned = DateTime.UtcNow
        };
        investigation.InvestigationAssignments = [assignment];

        var docket = new Docket
        {
            DocketID = 100,
            DocketNumber = "DCK-100",
            OBReferenceNumber = "OB-100",
            IncidentDate = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            FK_StationID = 1,
            Station = station,
            FK_InvestigationID = 200,
            Investigation = investigation,
            FK_DocketStatusID = 1,
            DocketStatus = docketStatus,
            FK_CreatedByUserID = actingUserId,
            CreatedByUser = actingUser
        };

        var bundle = new ProsecutorBundle
        {
            BundleID = 10,
            FK_DocketID = 100,
            Docket = docket,
            Title = "Bundle A",
            CreatedByUserID = actingUserId
        };

        var item = new ProsecutorBundleItem
        {
            ItemID = 501,
            FK_BundleID = 10,
            ProsecutorBundle = bundle,
            Section = "B-clip",
            Order = 1,
            Name = "Witness statement",
            IsPrivate = isPrivate,
            PrivacyReason = isPrivate ? "Already private" : null
        };

        db.Roles.Add(role);
        db.Stations.Add(station);
        db.InvestigationStatuses.Add(investigationStatus);
        db.DocketStatuses.Add(docketStatus);
        db.Users.Add(actingUser);
        db.Investigations.Add(investigation);
        db.InvestigationAssignments.Add(assignment);
        db.Dockets.Add(docket);
        db.ProsecutorBundles.Add(bundle);
        db.ProsecutorBundleItems.Add(item);
        db.SaveChanges();
    }

    private static DocketActionsController BuildController(ApplicationDbContext db, int userId)
    {
        var controller = new DocketActionsController(
            db,
            new FakeAuditService(),
            new FakeAiService(),
            new FakeSystemConfigService(),
            new FakeReportPdfService(),
            new FakeFileStorageService());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("userId", userId.ToString())
                ], "TestAuth"))
            }
        };

        return controller;
    }
}
