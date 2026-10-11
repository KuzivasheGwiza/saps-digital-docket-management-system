using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Controllers;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Tests.Support;

namespace SAPS.DDMS.API.Tests.UseCaseTests;

public class ProsecutorBundleWorkflowTests
{
    [Fact]
    public async Task ShareBundle_AssignsOneProsecutor_SealsManifest_AndCannotBeRepeated()
    {
        await using var db = BuildDb(nameof(ShareBundle_AssignsOneProsecutor_SealsManifest_AndCannotBeRepeated));
        Seed(db);
        var controller = BuildController(db, 10);

        var result = await controller.ShareBundle(50, new DocketActionsController.ShareBundleDto
        {
            ProsecutorUserID = 20,
            Note = "Ready for prosecution review"
        });

        Assert.IsType<OkObjectResult>(result);
        var bundle = await db.ProsecutorBundles.Include(item => item.Revisions).SingleAsync(item => item.BundleID == 50);
        Assert.Equal("Shared", bundle.Status);
        Assert.Equal(20, bundle.RecipientProsecutorUserID);
        Assert.NotNull(bundle.SharedAt);
        Assert.Equal(10, bundle.SharedByUserID);
        Assert.Equal(64, bundle.SharedManifestHash?.Length);
        Assert.Contains(bundle.Revisions!, revision => revision.Status == "Shared");
        Assert.Contains(await db.Notifications.ToListAsync(), notification => notification.FK_UserID == 20 && notification.PriorityLevel == "High");

        var repeated = await controller.ShareBundle(50, new DocketActionsController.ShareBundleDto { ProsecutorUserID = 20 });
        Assert.IsType<ConflictObjectResult>(repeated);
    }

    [Fact]
    public async Task SharedBundle_IsVisibleOnlyToAssignedProsecutor()
    {
        await using var db = BuildDb(nameof(SharedBundle_IsVisibleOnlyToAssignedProsecutor));
        Seed(db);
        var supervisor = BuildController(db, 10);
        await supervisor.ShareBundle(50, new DocketActionsController.ShareBundleDto { ProsecutorUserID = 20 });

        var assignedResult = await BuildController(db, 20).GetProsecutorBundle(50);
        var otherResult = await BuildController(db, 21).GetProsecutorBundle(50);

        Assert.IsType<OkObjectResult>(assignedResult);
        Assert.IsType<ForbidResult>(otherResult);
    }

    [Fact]
    public async Task ShareBundle_RejectsProsecutorFromAnotherStation()
    {
        await using var db = BuildDb(nameof(ShareBundle_RejectsProsecutorFromAnotherStation));
        Seed(db);
        var result = await BuildController(db, 10).ShareBundle(50,
            new DocketActionsController.ShareBundleDto { ProsecutorUserID = 22 });
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("different station", badRequest.Value?.ToString());
    }

    private static ApplicationDbContext BuildDb(string name) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options);

    private static void Seed(ApplicationDbContext db)
    {
        var supervisorRole = new Role { RoleID = 1, Name = "DSC Supervisor" };
        var prosecutorRole = new Role { RoleID = 2, Name = "Prosecutor" };
        var station = new Station { StationID = 1, StationName = "Pretoria Central" };
        var otherStation = new Station { StationID = 2, StationName = "Other Station" };
        db.Roles.AddRange(supervisorRole, prosecutorRole);
        db.Stations.AddRange(station, otherStation);
        db.Users.AddRange(
            User(10, "Supervisor", supervisorRole, station),
            User(20, "Assigned", prosecutorRole, station),
            User(21, "Other", prosecutorRole, station),
            User(22, "Remote", prosecutorRole, otherStation));
        var status = new DocketStatus { DocketStatusID = 1, StatusName = "PENDING_PROSECUTOR" };
        db.DocketStatuses.Add(status);
        var docket = new Docket
        {
            DocketID = 40, DocketNumber = "DCK-40", OBReferenceNumber = "OB-40",
            IncidentDate = DateTime.UtcNow, FK_StationID = 1, Station = station,
            FK_DocketStatusID = 1, DocketStatus = status, FK_CreatedByUserID = 10
        };
        db.Dockets.Add(docket);
        var bundle = new ProsecutorBundle
        {
            BundleID = 50, FK_DocketID = 40, Docket = docket, Title = "Case Bundle",
            Status = "Draft", Version = 1, CreatedByUserID = 10
        };
        db.ProsecutorBundles.Add(bundle);
        db.ProsecutorBundleItems.AddRange(
            new ProsecutorBundleItem { ItemID = 60, FK_BundleID = 50, Section = "A-clip", Order = 1, Name = "Cover" },
            new ProsecutorBundleItem { ItemID = 61, FK_BundleID = 50, Section = "B-clip", Order = 1, Name = "Witness address", IsPrivate = true, PrivacyReason = "Protected" });
        db.SaveChanges();
    }

    private static User User(int id, string name, Role role, Station station) => new()
    {
        UserID = id, FirstName = name, LastName = "User", EmailAddress = $"{name.ToLowerInvariant()}@test.local",
        PersonnelNumber = $"PN{id}", Username = $"user{id}", PasswordHash = "hash", FK_RoleID = role.RoleID,
        Role = role, FK_StationID = station.StationID, Station = station, IsActive = true, IsActivated = true,
        AccountStatus = "Active", IsLocked = false
    };

    private static DocketActionsController BuildController(ApplicationDbContext db, int userId)
    {
        var controller = new DocketActionsController(db, new FakeAuditService(), new FakeAiService(),
            new FakeSystemConfigService(), new FakeReportPdfService(), new FakeFileStorageService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", userId.ToString())], "TestAuth"))
            }
        };
        return controller;
    }
}
