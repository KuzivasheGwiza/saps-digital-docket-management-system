using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Helpers;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Tests.Support;

namespace SAPS.DDMS.API.Tests.UseCaseTests;

public class AuthServiceTests
{
    [Fact]
    public async Task Login_AcceptsTrimmedCaseInsensitiveEmail()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Login_AcceptsTrimmedCaseInsensitiveEmail))
            .Options;

        await using var db = new ApplicationDbContext(options);
        var user = ActiveUser();
        var users = new FakeUserRepository { UserByEmail = user };
        var audit = new FakeAuditService();
        var service = CreateService(db, users, audit);

        var result = await service.LoginAsync(new Models.DTOs.LoginDto
        {
            PersonnelNumber = "  Officer@Test.Local  ",
            Password = "ValidPassword1!"
        }, "127.0.0.1");

        Assert.True(result.Success, result.Message);
        Assert.Equal("officer@test.local", users.LastEmailLookup);
        Assert.NotNull(result.Data?.Token);
        Assert.Equal("P001", result.Data?.User?.PersonnelNumber);
        Assert.Contains(audit.Entries, entry => entry.ActionType == "LOGIN");
    }

    [Fact]
    public async Task Login_NormalizesPersonnelNumberBeforeLookup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Login_NormalizesPersonnelNumberBeforeLookup))
            .Options;

        await using var db = new ApplicationDbContext(options);
        var users = new FakeUserRepository { UserByUsername = ActiveUser() };
        var service = CreateService(db, users, new FakeAuditService());

        var result = await service.LoginAsync(new Models.DTOs.LoginDto
        {
            PersonnelNumber = "  p001  ",
            Password = "ValidPassword1!"
        }, "127.0.0.1");

        Assert.True(result.Success, result.Message);
        Assert.Equal("P001", users.LastUsernameLookup);
    }

    [Fact]
    public async Task ResetPassword_RejectsExpiredToken_AndClearsResetFields()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(ResetPassword_RejectsExpiredToken_AndClearsResetFields))
            .Options;

        await using var db = new ApplicationDbContext(options);
        var user = new User
        {
            UserID = 77,
            Username = "io77",
            EmailAddress = "io77@test.local",
            PasswordHash = PasswordHelper.HashPassword("OldPassword1!"),
            PasswordResetCode = PasswordHelper.HashToken("expired-token"),
            PasswordResetExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            IsActive = true,
            IsActivated = true,
            AccountStatus = "Active",
            FK_RoleID = 3,
            FK_StationID = 1
        };

        var users = new FakeUserRepository { UserByResetToken = user };
        var service = new AuthService(
            users,
            new JwtHelper(TestConfiguration.Build()),
            new FakeAuditService(),
            new FakeEmailService(),
            db,
            new FakeSmsService(),
            new FakeSystemConfigService(("PASSWORD_HISTORY_COUNT", "5")),
            TestConfiguration.Build());

        var result = await service.ResetPasswordAsync("expired-token", "NewPassword1!");

        Assert.False(result.Success);
        Assert.Equal("Invalid or expired reset token", result.Message);
        Assert.Null(user.PasswordResetCode);
        Assert.Null(user.PasswordResetExpiresAt);
    }

    [Fact]
    public async Task ForgotPassword_IssuesHashedSixDigitOtpByEmail()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(ForgotPassword_IssuesHashedSixDigitOtpByEmail))
            .Options;

        await using var db = new ApplicationDbContext(options);
        var user = ActiveUser();
        var users = new FakeUserRepository { UserByEmail = user };
        var email = new FakeEmailService();
        var audit = new FakeAuditService();
        var service = new AuthService(
            users,
            new JwtHelper(TestConfiguration.Build()),
            audit,
            email,
            db,
            new FakeSmsService(),
            new FakeSystemConfigService(("OTP_EXPIRY_MINUTES", "7"), ("PASSWORD_HISTORY_COUNT", "5")),
            TestConfiguration.Build());

        var before = DateTime.UtcNow;
        var result = await service.ForgotPasswordAsync("officer@test.local");

        Assert.True(result.Success, result.Message);
        Assert.Matches("^[0-9]{6}$", email.LastPasswordResetOtp!);
        Assert.Equal("officer@test.local", email.LastPasswordResetRecipient);
        Assert.Equal(7, email.LastPasswordResetExpiryMinutes);
        Assert.Equal(PasswordHelper.HashToken(email.LastPasswordResetOtp!), user.PasswordResetCode);
        Assert.InRange(user.PasswordResetExpiresAt!.Value, before.AddMinutes(7), DateTime.UtcNow.AddMinutes(7));
        Assert.Contains(audit.Entries, entry => entry.ActionType == "ISSUE_PASSWORD_RESET_OTP");
    }

    private static User ActiveUser() => new()
    {
        UserID = 42,
        FirstName = "Test",
        LastName = "Officer",
        Username = "P001",
        PersonnelNumber = "P001",
        EmailAddress = "officer@test.local",
        PasswordHash = PasswordHelper.HashPassword("ValidPassword1!"),
        IsActive = true,
        IsActivated = true,
        AccountStatus = "Active",
        FK_RoleID = 1,
        FK_StationID = 1,
        Role = new Role { RoleID = 1, Name = "System Administrator", RolePermissions = [] },
        Station = new Station { StationID = 1, StationName = "Test Station" }
    };

    private static AuthService CreateService(
        ApplicationDbContext db,
        FakeUserRepository users,
        FakeAuditService audit) => new(
            users,
            new JwtHelper(TestConfiguration.Build()),
            audit,
            new FakeEmailService(),
            db,
            new FakeSmsService(),
            new FakeSystemConfigService(
                ("TWO_FACTOR_REQUIRED", "false"),
                ("SESSION_TIMEOUT_MINUTES", "60")),
            TestConfiguration.Build());
}
