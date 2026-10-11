using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Controllers;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Tests.Support;

namespace SAPS.DDMS.API.Tests.UseCaseTests;

public class CourtOutcomeTests
{
    [Fact]
    public async Task RecordCourtOutcome_RequiresSharedBundleBeforeRecordingOutcome()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(RecordCourtOutcome_RequiresSharedBundleBeforeRecordingOutcome))
            .Options;

        await using var db = new ApplicationDbContext(options);
        SeedCourtOutcomeScenario(db, exportedBundleExists: false);

        var controller = new DocketActionsController(
            db,
            new FakeAuditService(),
            new FakeAiService(),
            new FakeSystemConfigService(),
            new FakeReportPdfService(),
            new FakeFileStorageService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("userId", "22")
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.RecordCourtOutcome(700, new DocketActionsController.CourtOutcomeDto
        {
            Outcome = "Withdrawn",
            CourtDate = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc),
            PresidingOfficer = "Adv. Molefe",
            Notes = "Case withdrawn pending further evidence"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("prosecutor bundle must be shared", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static void SeedCourtOutcomeScenario(ApplicationDbContext db, bool exportedBundleExists)
    {
        var role = new Role { RoleID = 2, Name = "DSC Supervisor" };
        var station = new Station { StationID = 1, StationName = "Pretoria Central" };
        var investigationStatus = new InvestigationStatus { InvestigationStatusID = 1, StatusName = "Active" };
        var user = new User
        {
            UserID = 22,
            Username = "dsc22",
            PersonnelNumber = "PN000022",
            FirstName = "Dsc",
            LastName = "Supervisor",
            FK_RoleID = 2,
            Role = role,
            FK_StationID = 1,
            Station = station,
            IsActive = true,
            IsActivated = true,
            AccountStatus = "Active",
            IsLocked = false,
            EmailAddress = "dsc22@test.local",
            PasswordHash = "hash"
        };

        var investigation = new Investigation
        {
            InvestigationID = 702,
            Title = "Court outcome investigation",
            FK_InvestigationStatusID = 1,
            InvestigationStatus = investigationStatus
        };
        investigation.InvestigationAssignments =
        [
            new InvestigationAssignment
            {
                AssignmentID = 703,
                FK_InvestigationID = 702,
                Investigation = investigation,
                FK_UserID = 22,
                User = user,
                DateAssigned = DateTime.UtcNow
            }
        ];

        var pendingStatus = new DocketStatus { DocketStatusID = 4, StatusName = "PENDING_PROSECUTOR" };
        var docket = new Docket
        {
            DocketID = 700,
            DocketNumber = "DCK-700",
            OBReferenceNumber = "OB-700",
            DateCreated = new DateTime(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc),
            IncidentDate = new DateTime(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc),
            FK_StationID = 1,
            Station = station,
            FK_InvestigationID = 702,
            Investigation = investigation,
            FK_CreatedByUserID = 22,
            CreatedByUser = user,
            FK_DocketStatusID = 4,
            DocketStatus = pendingStatus
        };

        db.Roles.Add(role);
        db.Stations.Add(station);
        db.InvestigationStatuses.Add(investigationStatus);
        db.Users.Add(user);
        db.Investigations.Add(investigation);
        db.DocketStatuses.Add(pendingStatus);
        db.Dockets.Add(docket);

        if (exportedBundleExists)
        {
            db.ProsecutorBundles.Add(new ProsecutorBundle
            {
                BundleID = 704,
                FK_DocketID = 700,
                Docket = docket,
                Title = "Bundle 700",
                Status = "Shared",
                RecipientProsecutorUserID = 99,
                CreatedByUserID = 22
            });
        }

        db.SaveChanges();
    }
}
