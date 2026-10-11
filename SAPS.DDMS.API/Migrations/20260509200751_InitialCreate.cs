using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SAPS.DDMS.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocketStatuses",
                columns: table => new
                {
                    DocketStatusID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatusName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocketStatuses", x => x.DocketStatusID);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceLocations",
                columns: table => new
                {
                    EvidenceLocationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PhysicalAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StorageRequirements = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceLocations", x => x.EvidenceLocationID);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceTypes",
                columns: table => new
                {
                    EvidenceTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceTypes", x => x.EvidenceTypeID);
                });

            migrationBuilder.CreateTable(
                name: "GeoLocations",
                columns: table => new
                {
                    GeoID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Latitude = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Longitude = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AreaName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeoLocations", x => x.GeoID);
                });

            migrationBuilder.CreateTable(
                name: "IncidentTypes",
                columns: table => new
                {
                    IncidentTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTypes", x => x.IncidentTypeID);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationStatuses",
                columns: table => new
                {
                    InvestigationStatusID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatusName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationStatuses", x => x.InvestigationStatusID);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    PermissionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.PermissionID);
                });

            migrationBuilder.CreateTable(
                name: "PersonRoles",
                columns: table => new
                {
                    PersonRoleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonRoles", x => x.PersonRoleID);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Permissions = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleID);
                });

            migrationBuilder.CreateTable(
                name: "Stations",
                columns: table => new
                {
                    StationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Province = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stations", x => x.StationID);
                });

            migrationBuilder.CreateTable(
                name: "Investigations",
                columns: table => new
                {
                    InvestigationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FK_InvestigationStatusID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Investigations", x => x.InvestigationID);
                    table.ForeignKey(
                        name: "FK_Investigations_InvestigationStatuses_FK_InvestigationStatusID",
                        column: x => x.FK_InvestigationStatusID,
                        principalTable: "InvestigationStatuses",
                        principalColumn: "InvestigationStatusID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "int", nullable: false),
                    PermissionID = table.Column<int>(type: "int", nullable: false),
                    AccessLevel = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleID, x.PermissionID });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionID",
                        column: x => x.PermissionID,
                        principalTable: "Permissions",
                        principalColumn: "PermissionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleID",
                        column: x => x.RoleID,
                        principalTable: "Roles",
                        principalColumn: "RoleID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PersonnelNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CommunicationPreference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordResetCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActivated = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserID = table.Column<int>(type: "int", nullable: true),
                    FK_RoleID = table.Column<int>(type: "int", nullable: false),
                    FK_StationID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_Users_Roles_FK_RoleID",
                        column: x => x.FK_RoleID,
                        principalTable: "Roles",
                        principalColumn: "RoleID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Users_Stations_FK_StationID",
                        column: x => x.FK_StationID,
                        principalTable: "Stations",
                        principalColumn: "StationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    IncidentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurrenceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LocationType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FK_GeoID = table.Column<int>(type: "int", nullable: true),
                    FK_IncidentTypeID = table.Column<int>(type: "int", nullable: true),
                    FK_UserID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.IncidentID);
                    table.ForeignKey(
                        name: "FK_Incidents_GeoLocations_FK_GeoID",
                        column: x => x.FK_GeoID,
                        principalTable: "GeoLocations",
                        principalColumn: "GeoID");
                    table.ForeignKey(
                        name: "FK_Incidents_IncidentTypes_FK_IncidentTypeID",
                        column: x => x.FK_IncidentTypeID,
                        principalTable: "IncidentTypes",
                        principalColumn: "IncidentTypeID");
                    table.ForeignKey(
                        name: "FK_Incidents_Users_FK_UserID",
                        column: x => x.FK_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationAssignments",
                columns: table => new
                {
                    AssignmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DateAssigned = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FK_UserID = table.Column<int>(type: "int", nullable: false),
                    FK_InvestigationID = table.Column<int>(type: "int", nullable: false),
                    FK_AssignedByUserID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationAssignments", x => x.AssignmentID);
                    table.ForeignKey(
                        name: "FK_InvestigationAssignments_Investigations_FK_InvestigationID",
                        column: x => x.FK_InvestigationID,
                        principalTable: "Investigations",
                        principalColumn: "InvestigationID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvestigationAssignments_Users_FK_AssignedByUserID",
                        column: x => x.FK_AssignedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestigationAssignments_Users_FK_UserID",
                        column: x => x.FK_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationNotes",
                columns: table => new
                {
                    NoteID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FK_InvestigationID = table.Column<int>(type: "int", nullable: false),
                    FK_AuthorUserID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationNotes", x => x.NoteID);
                    table.ForeignKey(
                        name: "FK_InvestigationNotes_Investigations_FK_InvestigationID",
                        column: x => x.FK_InvestigationID,
                        principalTable: "Investigations",
                        principalColumn: "InvestigationID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvestigationNotes_Users_FK_AuthorUserID",
                        column: x => x.FK_AuthorUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsEmailSent = table.Column<bool>(type: "bit", nullable: false),
                    IsSMSSent = table.Column<bool>(type: "bit", nullable: false),
                    PriorityLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FK_UserID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationID);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_FK_UserID",
                        column: x => x.FK_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Persons",
                columns: table => new
                {
                    PersonID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IDNumber = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhysicalAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FK_CreatedByUserID = table.Column<int>(type: "int", nullable: false),
                    FK_PersonRoleID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.PersonID);
                    table.ForeignKey(
                        name: "FK_Persons_PersonRoles_FK_PersonRoleID",
                        column: x => x.FK_PersonRoleID,
                        principalTable: "PersonRoles",
                        principalColumn: "PersonRoleID");
                    table.ForeignKey(
                        name: "FK_Persons_Users_FK_CreatedByUserID",
                        column: x => x.FK_CreatedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    SessionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Login = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActivity = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Logout = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FK_UserID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.SessionID);
                    table.ForeignKey(
                        name: "FK_Sessions_Users_FK_UserID",
                        column: x => x.FK_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAuditLogs",
                columns: table => new
                {
                    UserAuditLogID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AffectedEntity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AffectedEntityID = table.Column<int>(type: "int", nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IPAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FK_UserID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAuditLogs", x => x.UserAuditLogID);
                    table.ForeignKey(
                        name: "FK_UserAuditLogs_Users_FK_UserID",
                        column: x => x.FK_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dockets",
                columns: table => new
                {
                    DocketID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocketNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OBReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FICReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateAllocated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriorityLevel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IncidentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModusOperandi = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DocketSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FK_IncidentID = table.Column<int>(type: "int", nullable: true),
                    FK_DocketStatusID = table.Column<int>(type: "int", nullable: false),
                    FK_StationID = table.Column<int>(type: "int", nullable: false),
                    FK_CreatedByUserID = table.Column<int>(type: "int", nullable: false),
                    FK_InvestigationID = table.Column<int>(type: "int", nullable: true),
                    InvestigationID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dockets", x => x.DocketID);
                    table.ForeignKey(
                        name: "FK_Dockets_DocketStatuses_FK_DocketStatusID",
                        column: x => x.FK_DocketStatusID,
                        principalTable: "DocketStatuses",
                        principalColumn: "DocketStatusID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dockets_Incidents_FK_IncidentID",
                        column: x => x.FK_IncidentID,
                        principalTable: "Incidents",
                        principalColumn: "IncidentID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dockets_Investigations_FK_InvestigationID",
                        column: x => x.FK_InvestigationID,
                        principalTable: "Investigations",
                        principalColumn: "InvestigationID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dockets_Investigations_InvestigationID",
                        column: x => x.InvestigationID,
                        principalTable: "Investigations",
                        principalColumn: "InvestigationID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Dockets_Stations_FK_StationID",
                        column: x => x.FK_StationID,
                        principalTable: "Stations",
                        principalColumn: "StationID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dockets_Users_FK_CreatedByUserID",
                        column: x => x.FK_CreatedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteAttachments",
                columns: table => new
                {
                    AttachmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileSizeBytes = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FK_NoteID = table.Column<int>(type: "int", nullable: false),
                    FK_UserID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteAttachments", x => x.AttachmentID);
                    table.ForeignKey(
                        name: "FK_NoteAttachments_InvestigationNotes_FK_NoteID",
                        column: x => x.FK_NoteID,
                        principalTable: "InvestigationNotes",
                        principalColumn: "NoteID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NoteAttachments_Users_FK_UserID",
                        column: x => x.FK_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteVersions",
                columns: table => new
                {
                    VersionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    NoteTextBefore = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NoteTextAfter = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FK_NoteID = table.Column<int>(type: "int", nullable: false),
                    FK_UserID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteVersions", x => x.VersionID);
                    table.ForeignKey(
                        name: "FK_NoteVersions_InvestigationNotes_FK_NoteID",
                        column: x => x.FK_NoteID,
                        principalTable: "InvestigationNotes",
                        principalColumn: "NoteID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NoteVersions_Users_FK_UserID",
                        column: x => x.FK_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionLogs",
                columns: table => new
                {
                    SessionLogID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActionType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AffectedEntity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AffectedRecordID = table.Column<int>(type: "int", nullable: true),
                    FK_SessionID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionLogs", x => x.SessionLogID);
                    table.ForeignKey(
                        name: "FK_SessionLogs_Sessions_FK_SessionID",
                        column: x => x.FK_SessionID,
                        principalTable: "Sessions",
                        principalColumn: "SessionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionTimers",
                columns: table => new
                {
                    SessionTimerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: true),
                    RemainingTime = table.Column<double>(type: "float", nullable: true),
                    FK_SessionID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionTimers", x => x.SessionTimerID);
                    table.ForeignKey(
                        name: "FK_SessionTimers_Sessions_FK_SessionID",
                        column: x => x.FK_SessionID,
                        principalTable: "Sessions",
                        principalColumn: "SessionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocketPersonLinks",
                columns: table => new
                {
                    LinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FK_DocketID = table.Column<int>(type: "int", nullable: false),
                    FK_PersonID = table.Column<int>(type: "int", nullable: false),
                    FK_LinkedByUserID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocketPersonLinks", x => x.LinkID);
                    table.ForeignKey(
                        name: "FK_DocketPersonLinks_Dockets_FK_DocketID",
                        column: x => x.FK_DocketID,
                        principalTable: "Dockets",
                        principalColumn: "DocketID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocketPersonLinks_Persons_FK_PersonID",
                        column: x => x.FK_PersonID,
                        principalTable: "Persons",
                        principalColumn: "PersonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocketPersonLinks_Users_FK_LinkedByUserID",
                        column: x => x.FK_LinkedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocketPrivacyFlags",
                columns: table => new
                {
                    FlagID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FieldName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemovalReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FK_DocketID = table.Column<int>(type: "int", nullable: false),
                    FK_Applied_By_UserID = table.Column<int>(type: "int", nullable: false),
                    FK_Applied_For_UserID = table.Column<int>(type: "int", nullable: false),
                    FK_Removed_By_UserID = table.Column<int>(type: "int", nullable: true),
                    FK_Removed_For_UserID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocketPrivacyFlags", x => x.FlagID);
                    table.ForeignKey(
                        name: "FK_DocketPrivacyFlags_Dockets_FK_DocketID",
                        column: x => x.FK_DocketID,
                        principalTable: "Dockets",
                        principalColumn: "DocketID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocketPrivacyFlags_Users_FK_Applied_By_UserID",
                        column: x => x.FK_Applied_By_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocketPrivacyFlags_Users_FK_Applied_For_UserID",
                        column: x => x.FK_Applied_For_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocketPrivacyFlags_Users_FK_Removed_By_UserID",
                        column: x => x.FK_Removed_By_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocketPrivacyFlags_Users_FK_Removed_For_UserID",
                        column: x => x.FK_Removed_For_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocketTimers",
                columns: table => new
                {
                    DocketTimerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationLimit = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    FK_DocketID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocketTimers", x => x.DocketTimerID);
                    table.ForeignKey(
                        name: "FK_DocketTimers_Dockets_FK_DocketID",
                        column: x => x.FK_DocketID,
                        principalTable: "Dockets",
                        principalColumn: "DocketID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Evidences",
                columns: table => new
                {
                    EvidenceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    DateCollected = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CollectionMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StateOfItem = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EvidenceReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FK_EvidenceTypeID = table.Column<int>(type: "int", nullable: false),
                    FK_EvidenceLocationID = table.Column<int>(type: "int", nullable: false),
                    FK_InvestigationID = table.Column<int>(type: "int", nullable: false),
                    FK_UserID = table.Column<int>(type: "int", nullable: false),
                    FK_DocketID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidences", x => x.EvidenceID);
                    table.ForeignKey(
                        name: "FK_Evidences_Dockets_FK_DocketID",
                        column: x => x.FK_DocketID,
                        principalTable: "Dockets",
                        principalColumn: "DocketID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Evidences_EvidenceLocations_FK_EvidenceLocationID",
                        column: x => x.FK_EvidenceLocationID,
                        principalTable: "EvidenceLocations",
                        principalColumn: "EvidenceLocationID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evidences_EvidenceTypes_FK_EvidenceTypeID",
                        column: x => x.FK_EvidenceTypeID,
                        principalTable: "EvidenceTypes",
                        principalColumn: "EvidenceTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evidences_Investigations_FK_InvestigationID",
                        column: x => x.FK_InvestigationID,
                        principalTable: "Investigations",
                        principalColumn: "InvestigationID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Evidences_Users_FK_UserID",
                        column: x => x.FK_UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "DocketStatuses",
                columns: new[] { "DocketStatusID", "StatusName" },
                values: new object[,]
                {
                    { 1, "OPEN" },
                    { 2, "ALLOCATED" },
                    { 3, "UNDER_INVESTIGATION" },
                    { 4, "PENDING_PROSECUTOR" },
                    { 5, "CLOSED" },
                    { 6, "ARCHIVED" }
                });

            migrationBuilder.InsertData(
                table: "EvidenceLocations",
                columns: new[] { "EvidenceLocationID", "LocationName", "PhysicalAddress", "StorageRequirements" },
                values: new object[,]
                {
                    { 1, "Evidence Room - Main", "SAPS HQ", "Climate controlled" },
                    { 2, "Evidence Room - Secondary", "SAPS Annex", "Standard" },
                    { 3, "Forensics Lab", "Forensics Division", "Controlled access" },
                    { 4, "Digital Evidence Lab", "Cyber Unit", "Static-safe" },
                    { 5, "With IO", "In custody of IO", "Officer responsible" },
                    { 6, "Court", "Submitted to court", "Court registry" }
                });

            migrationBuilder.InsertData(
                table: "EvidenceTypes",
                columns: new[] { "EvidenceTypeID", "Name" },
                values: new object[,]
                {
                    { 1, "Physical" },
                    { 2, "Digital" },
                    { 3, "Document" },
                    { 4, "Biological" },
                    { 5, "Fingerprint" },
                    { 6, "Photographic" },
                    { 7, "Audio" },
                    { 8, "Video" }
                });

            migrationBuilder.InsertData(
                table: "IncidentTypes",
                columns: new[] { "IncidentTypeID", "Description", "TypeName" },
                values: new object[,]
                {
                    { 1, "Theft by force", "Robbery" },
                    { 2, "Breaking and entering", "Burglary" },
                    { 3, "Grievous bodily harm", "Assault GBH" },
                    { 4, "Unlawful killing", "Murder" },
                    { 5, "Sexual assault", "Sexual Offense" },
                    { 6, "Financial crime", "Fraud" },
                    { 7, "Financial crime", "Corruption" },
                    { 8, "Financial crime", "Money Laundering" },
                    { 9, "Property theft", "Theft" },
                    { 10, "Narcotics offense", "Drug Related" }
                });

            migrationBuilder.InsertData(
                table: "InvestigationStatuses",
                columns: new[] { "InvestigationStatusID", "StatusName" },
                values: new object[,]
                {
                    { 1, "OPEN" },
                    { 2, "IN_PROGRESS" },
                    { 3, "COMPLETED" },
                    { 4, "SUSPENDED" }
                });

            migrationBuilder.InsertData(
                table: "PersonRoles",
                columns: new[] { "PersonRoleID", "Code", "Description", "IsActive", "RoleName" },
                values: new object[,]
                {
                    { 1, "CMP", "Person who reports the crime", true, "Complainant" },
                    { 2, "SUS", "Person accused of the crime", true, "Suspect" },
                    { 3, "WIT", "Person who witnessed the incident", true, "Witness" },
                    { 4, "VIC", "Person who suffered harm", true, "Victim" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "RoleID", "Description", "Name", "Permissions" },
                values: new object[,]
                {
                    { 1, "Full system access", "System Administrator", null },
                    { 2, "Supervises docket allocation", "DSC Supervisor", null },
                    { 3, "Conducts investigations", "Investigating Officer", null },
                    { 4, "Creates and manages dockets", "CSC Officer", null },
                    { 5, "Manages docket administration", "Docket Administrator", null },
                    { 6, "Maintains docket records", "Records Clerk", null },
                    { 7, "Reviews prosecutor bundles", "Prosecutor", null }
                });

            migrationBuilder.InsertData(
                table: "Stations",
                columns: new[] { "StationID", "Code", "Province", "Region", "StationName" },
                values: new object[,]
                {
                    { 1, null, "Gauteng", "Tshwane", "Pretoria Central" },
                    { 2, null, "Gauteng", "Johannesburg", "Johannesburg Central" },
                    { 3, null, "Western Cape", "Cape Town", "Cape Town Central" },
                    { 4, null, "KwaZulu-Natal", "Durban", "Durban Central" },
                    { 5, null, "Eastern Cape", "Nelson Mandela Bay", "Port Elizabeth Central" },
                    { 6, null, "Gauteng", "Tshwane", "Sunnyside" },
                    { 7, null, "Gauteng", "Tshwane", "Brooklyn" },
                    { 8, null, "Gauteng", "Johannesburg", "Sandton" },
                    { 9, null, "Gauteng", "Johannesburg", "Soweto" },
                    { 10, null, "Western Cape", "Cape Town", "Mitchells Plain" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserID", "CommunicationPreference", "CreatedByUserID", "DateCreated", "EmailAddress", "FK_RoleID", "FK_StationID", "FailedLoginAttempts", "FirstName", "IsActivated", "IsActive", "IsLocked", "LastLoginDate", "LastName", "PasswordHash", "PasswordResetCode", "PersonnelNumber", "PhoneNumber", "Rank", "Username" },
                values: new object[] { 1, null, null, new DateTime(2026, 5, 9, 20, 7, 49, 755, DateTimeKind.Utc).AddTicks(3564), "admin@saps.gov.za", 1, 1, 0, "System", true, true, false, null, "Administrator", "$2a$12$9LkgrFL1aJP1AKD31CTviuo3sSPmDn1Z/WfDVf5Ujfq8KdI.LQ3t2", null, "ADMIN001", null, "Senior Officer", "ADMIN001" });

            migrationBuilder.CreateIndex(
                name: "IX_DocketPersonLinks_FK_DocketID",
                table: "DocketPersonLinks",
                column: "FK_DocketID");

            migrationBuilder.CreateIndex(
                name: "IX_DocketPersonLinks_FK_LinkedByUserID",
                table: "DocketPersonLinks",
                column: "FK_LinkedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_DocketPersonLinks_FK_PersonID",
                table: "DocketPersonLinks",
                column: "FK_PersonID");

            migrationBuilder.CreateIndex(
                name: "IX_DocketPrivacyFlags_FK_Applied_By_UserID",
                table: "DocketPrivacyFlags",
                column: "FK_Applied_By_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_DocketPrivacyFlags_FK_Applied_For_UserID",
                table: "DocketPrivacyFlags",
                column: "FK_Applied_For_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_DocketPrivacyFlags_FK_DocketID",
                table: "DocketPrivacyFlags",
                column: "FK_DocketID");

            migrationBuilder.CreateIndex(
                name: "IX_DocketPrivacyFlags_FK_Removed_By_UserID",
                table: "DocketPrivacyFlags",
                column: "FK_Removed_By_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_DocketPrivacyFlags_FK_Removed_For_UserID",
                table: "DocketPrivacyFlags",
                column: "FK_Removed_For_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Dockets_DocketNumber",
                table: "Dockets",
                column: "DocketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dockets_FK_CreatedByUserID",
                table: "Dockets",
                column: "FK_CreatedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_Dockets_FK_DocketStatusID",
                table: "Dockets",
                column: "FK_DocketStatusID");

            migrationBuilder.CreateIndex(
                name: "IX_Dockets_FK_IncidentID",
                table: "Dockets",
                column: "FK_IncidentID",
                unique: true,
                filter: "[FK_IncidentID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Dockets_FK_InvestigationID",
                table: "Dockets",
                column: "FK_InvestigationID",
                unique: true,
                filter: "[FK_InvestigationID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Dockets_FK_StationID",
                table: "Dockets",
                column: "FK_StationID");

            migrationBuilder.CreateIndex(
                name: "IX_Dockets_InvestigationID",
                table: "Dockets",
                column: "InvestigationID");

            migrationBuilder.CreateIndex(
                name: "IX_Dockets_OBReferenceNumber",
                table: "Dockets",
                column: "OBReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocketTimers_FK_DocketID",
                table: "DocketTimers",
                column: "FK_DocketID");

            migrationBuilder.CreateIndex(
                name: "IX_Evidences_FK_DocketID",
                table: "Evidences",
                column: "FK_DocketID");

            migrationBuilder.CreateIndex(
                name: "IX_Evidences_FK_EvidenceLocationID",
                table: "Evidences",
                column: "FK_EvidenceLocationID");

            migrationBuilder.CreateIndex(
                name: "IX_Evidences_FK_EvidenceTypeID",
                table: "Evidences",
                column: "FK_EvidenceTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_Evidences_FK_InvestigationID",
                table: "Evidences",
                column: "FK_InvestigationID");

            migrationBuilder.CreateIndex(
                name: "IX_Evidences_FK_UserID",
                table: "Evidences",
                column: "FK_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_FK_GeoID",
                table: "Incidents",
                column: "FK_GeoID");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_FK_IncidentTypeID",
                table: "Incidents",
                column: "FK_IncidentTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_FK_UserID",
                table: "Incidents",
                column: "FK_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationAssignments_FK_AssignedByUserID",
                table: "InvestigationAssignments",
                column: "FK_AssignedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationAssignments_FK_InvestigationID",
                table: "InvestigationAssignments",
                column: "FK_InvestigationID");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationAssignments_FK_UserID",
                table: "InvestigationAssignments",
                column: "FK_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationNotes_FK_AuthorUserID",
                table: "InvestigationNotes",
                column: "FK_AuthorUserID");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationNotes_FK_InvestigationID",
                table: "InvestigationNotes",
                column: "FK_InvestigationID");

            migrationBuilder.CreateIndex(
                name: "IX_Investigations_FK_InvestigationStatusID",
                table: "Investigations",
                column: "FK_InvestigationStatusID");

            migrationBuilder.CreateIndex(
                name: "IX_NoteAttachments_FK_NoteID",
                table: "NoteAttachments",
                column: "FK_NoteID");

            migrationBuilder.CreateIndex(
                name: "IX_NoteAttachments_FK_UserID",
                table: "NoteAttachments",
                column: "FK_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersions_FK_NoteID",
                table: "NoteVersions",
                column: "FK_NoteID");

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersions_FK_UserID",
                table: "NoteVersions",
                column: "FK_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_FK_UserID",
                table: "Notifications",
                column: "FK_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_FK_CreatedByUserID",
                table: "Persons",
                column: "FK_CreatedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_FK_PersonRoleID",
                table: "Persons",
                column: "FK_PersonRoleID");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionID",
                table: "RolePermissions",
                column: "PermissionID");

            migrationBuilder.CreateIndex(
                name: "IX_SessionLogs_FK_SessionID",
                table: "SessionLogs",
                column: "FK_SessionID");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_FK_UserID",
                table: "Sessions",
                column: "FK_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_SessionTimers_FK_SessionID",
                table: "SessionTimers",
                column: "FK_SessionID");

            migrationBuilder.CreateIndex(
                name: "IX_UserAuditLogs_FK_UserID",
                table: "UserAuditLogs",
                column: "FK_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailAddress",
                table: "Users",
                column: "EmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_FK_RoleID",
                table: "Users",
                column: "FK_RoleID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_FK_StationID",
                table: "Users",
                column: "FK_StationID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocketPersonLinks");

            migrationBuilder.DropTable(
                name: "DocketPrivacyFlags");

            migrationBuilder.DropTable(
                name: "DocketTimers");

            migrationBuilder.DropTable(
                name: "Evidences");

            migrationBuilder.DropTable(
                name: "InvestigationAssignments");

            migrationBuilder.DropTable(
                name: "NoteAttachments");

            migrationBuilder.DropTable(
                name: "NoteVersions");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "SessionLogs");

            migrationBuilder.DropTable(
                name: "SessionTimers");

            migrationBuilder.DropTable(
                name: "UserAuditLogs");

            migrationBuilder.DropTable(
                name: "Persons");

            migrationBuilder.DropTable(
                name: "Dockets");

            migrationBuilder.DropTable(
                name: "EvidenceLocations");

            migrationBuilder.DropTable(
                name: "EvidenceTypes");

            migrationBuilder.DropTable(
                name: "InvestigationNotes");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "PersonRoles");

            migrationBuilder.DropTable(
                name: "DocketStatuses");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "Investigations");

            migrationBuilder.DropTable(
                name: "GeoLocations");

            migrationBuilder.DropTable(
                name: "IncidentTypes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "InvestigationStatuses");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Stations");
        }
    }
}
