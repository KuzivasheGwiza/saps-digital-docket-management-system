using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Helpers;

namespace SAPS.DDMS.API.Data
{
    public static class DatabaseBootstrapper
    {
        public static async Task<bool> IsOperationalSchemaCurrentAsync(ApplicationDbContext db)
        {
            // Avoid replaying the full legacy schema-convergence script on every
            // API launch. The probe covers the newest tables, columns, index, and
            // immutability trigger; an older installation still falls through to
            // the idempotent bootstrap below.
            var isCurrent = await db.Database.SqlQueryRaw<int>(@"
SELECT CASE WHEN
       OBJECT_ID(N'dbo.SystemSettings', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.BackupRecords', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.SmsMessages', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Feedback', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Users', N'AccountStatus') IS NOT NULL
   AND COL_LENGTH(N'dbo.Dockets', N'CaseType') IS NOT NULL
   AND COL_LENGTH(N'dbo.Dockets', N'SensitivityLevel') IS NOT NULL
   AND COL_LENGTH(N'dbo.Incidents', N'CustomIncidentType') IS NOT NULL
   AND COL_LENGTH(N'dbo.ProsecutorBundles', N'RecipientProsecutorUserID') IS NOT NULL
   AND COL_LENGTH(N'dbo.ProsecutorBundles', N'SharedManifestHash') IS NOT NULL
   AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.CK_ProsecutorBundles_Status_Allowed')) LIKE N'%Shared%'
   AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.CK_ProsecutorBundleRevisions_Status_Allowed')) LIKE N'%Shared%'
   AND EXISTS (SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.UserAuditLogs')
                  AND name = N'IX_UserAuditLogs_Timestamp')
   AND OBJECT_ID(N'dbo.TR_UserAuditLogs_Immutable', N'TR') IS NOT NULL
    THEN 1 ELSE 0 END AS [Value]
").SingleAsync();

            return isCurrent == 1;
        }

        public static async Task EnsureOperationalSchemaAsync(ApplicationDbContext db)
        {
            // Schema convergence can include indexed-column backfills on older
            // installations. Give those operations enough time to finish atomically.
            db.Database.SetCommandTimeout(TimeSpan.FromSeconds(120));

            await db.Database.ExecuteSqlRawAsync(@"
IF EXISTS (SELECT 1 FROM sys.databases WHERE database_id = DB_ID() AND is_auto_close_on = 1)
    ALTER DATABASE CURRENT SET AUTO_CLOSE OFF;

IF COL_LENGTH('dbo.Users', 'AccountStatus') IS NULL
    ALTER TABLE dbo.Users ADD AccountStatus nvarchar(50) NOT NULL CONSTRAINT DF_Users_AccountStatus DEFAULT('Active');
IF COL_LENGTH('dbo.Users', 'NotificationPreferences') IS NULL
    ALTER TABLE dbo.Users ADD NotificationPreferences nvarchar(256) NULL;
IF COL_LENGTH('dbo.Users', 'PasswordResetExpiresAt') IS NULL
    ALTER TABLE dbo.Users ADD PasswordResetExpiresAt datetime2 NULL;
IF COL_LENGTH('dbo.Users', 'OtpCodeHash') IS NULL
    ALTER TABLE dbo.Users ADD OtpCodeHash nvarchar(256) NULL;
IF COL_LENGTH('dbo.Users', 'OtpExpiresAt') IS NULL
    ALTER TABLE dbo.Users ADD OtpExpiresAt datetime2 NULL;
IF COL_LENGTH('dbo.Users', 'OtpRequestedAt') IS NULL
    ALTER TABLE dbo.Users ADD OtpRequestedAt datetime2 NULL;
IF COL_LENGTH('dbo.Users', 'IsTwoFactorEnabled') IS NULL
    ALTER TABLE dbo.Users ADD IsTwoFactorEnabled bit NOT NULL CONSTRAINT DF_Users_IsTwoFactorEnabled DEFAULT(0);
");

            // The project predates EF migration tracking in several deployed databases.
            // Keep these additions idempotent so an existing database converges to the
            // current model without dropping operational data.
            await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Dockets', 'DocketArchivedAt') IS NULL
    ALTER TABLE dbo.Dockets ADD DocketArchivedAt datetime2 NULL;
IF COL_LENGTH('dbo.Dockets', 'DocketClosedAt') IS NULL
    ALTER TABLE dbo.Dockets ADD DocketClosedAt datetime2 NULL;
IF COL_LENGTH('dbo.Dockets', 'ReopenedAt') IS NULL
    ALTER TABLE dbo.Dockets ADD ReopenedAt datetime2 NULL;
IF COL_LENGTH('dbo.Dockets', 'ComplainantFullName') IS NULL
    ALTER TABLE dbo.Dockets ADD ComplainantFullName nvarchar(256) NULL;
IF COL_LENGTH('dbo.Dockets', 'ComplainantContactNumber') IS NULL
    ALTER TABLE dbo.Dockets ADD ComplainantContactNumber nvarchar(30) NULL;
IF COL_LENGTH('dbo.Dockets', 'CaseType') IS NULL
    ALTER TABLE dbo.Dockets ADD CaseType nvarchar(50) NULL;
IF COL_LENGTH('dbo.Dockets', 'SensitivityLevel') IS NULL
    ALTER TABLE dbo.Dockets ADD SensitivityLevel nvarchar(30) NOT NULL CONSTRAINT DF_Dockets_SensitivityLevel DEFAULT('NORMAL');
IF COL_LENGTH('dbo.Incidents', 'CustomIncidentType') IS NULL
    ALTER TABLE dbo.Incidents ADD CustomIncidentType nvarchar(100) NULL;

-- Older EF metadata created a second shadow InvestigationID relationship.
-- Move its valid values into the canonical FK and retire it without losing data.
IF COL_LENGTH('dbo.Dockets', 'InvestigationID') IS NOT NULL
   AND COL_LENGTH('dbo.Dockets', 'FK_InvestigationID') IS NOT NULL
BEGIN
    -- Defer compilation of the legacy-column reference. SQL Server otherwise
    -- binds it even when this IF branch is false on an already-upgraded schema.
    EXEC sys.sp_executesql N'
        UPDATE docket
           SET FK_InvestigationID = docket.InvestigationID
          FROM dbo.Dockets docket
          INNER JOIN dbo.Investigations investigation
                  ON investigation.InvestigationID = docket.InvestigationID
         WHERE docket.FK_InvestigationID IS NULL;';

    IF NOT EXISTS (SELECT 1 FROM dbo.Dockets WHERE FK_InvestigationID IS NULL)
    BEGIN
        DECLARE @legacyInvestigationSql nvarchar(max);
        DECLARE @legacyInvestigationFk sysname;
        SELECT TOP (1) @legacyInvestigationFk = foreignKey.name
          FROM sys.foreign_keys foreignKey
          INNER JOIN sys.foreign_key_columns columnLink
                  ON columnLink.constraint_object_id = foreignKey.object_id
         WHERE foreignKey.parent_object_id = OBJECT_ID(N'dbo.Dockets')
           AND COL_NAME(columnLink.parent_object_id, columnLink.parent_column_id) = N'InvestigationID';
        IF @legacyInvestigationFk IS NOT NULL
        BEGIN
            SET @legacyInvestigationSql = N'ALTER TABLE dbo.Dockets DROP CONSTRAINT '
                + QUOTENAME(@legacyInvestigationFk);
            EXEC sys.sp_executesql @legacyInvestigationSql;
        END

        DECLARE @legacyInvestigationIndex sysname;
        SELECT TOP (1) @legacyInvestigationIndex = indexInfo.name
          FROM sys.indexes indexInfo
          INNER JOIN sys.index_columns indexColumn
                  ON indexColumn.object_id = indexInfo.object_id
                 AND indexColumn.index_id = indexInfo.index_id
         WHERE indexInfo.object_id = OBJECT_ID(N'dbo.Dockets')
           AND COL_NAME(indexColumn.object_id, indexColumn.column_id) = N'InvestigationID'
           AND indexInfo.is_primary_key = 0
           AND indexInfo.is_unique_constraint = 0;
        IF @legacyInvestigationIndex IS NOT NULL
        BEGIN
            SET @legacyInvestigationSql = N'DROP INDEX '
                + QUOTENAME(@legacyInvestigationIndex) + N' ON dbo.Dockets';
            EXEC sys.sp_executesql @legacyInvestigationSql;
        END

        DECLARE @legacyInvestigationDefault sysname;
        SELECT @legacyInvestigationDefault = defaultConstraint.name
          FROM sys.default_constraints defaultConstraint
          INNER JOIN sys.columns columnInfo
                  ON columnInfo.object_id = defaultConstraint.parent_object_id
                 AND columnInfo.column_id = defaultConstraint.parent_column_id
         WHERE defaultConstraint.parent_object_id = OBJECT_ID(N'dbo.Dockets')
           AND columnInfo.name = N'InvestigationID';
        IF @legacyInvestigationDefault IS NOT NULL
        BEGIN
            SET @legacyInvestigationSql = N'ALTER TABLE dbo.Dockets DROP CONSTRAINT '
                + QUOTENAME(@legacyInvestigationDefault);
            EXEC sys.sp_executesql @legacyInvestigationSql;
        END

        ALTER TABLE dbo.Dockets DROP COLUMN InvestigationID;
    END
    ELSE
    BEGIN
        -- Preserve unresolved legacy data, but do not let the unused column
        -- prevent new canonical docket rows from being inserted.
        ALTER TABLE dbo.Dockets ALTER COLUMN InvestigationID int NULL;
    END
END;

IF COL_LENGTH('dbo.Evidences', 'SignatureMethod') IS NULL
    ALTER TABLE dbo.Evidences ADD SignatureMethod nvarchar(50) NULL;
IF COL_LENGTH('dbo.Evidences', 'PayloadHash') IS NULL
    ALTER TABLE dbo.Evidences ADD PayloadHash nvarchar(256) NULL;

IF COL_LENGTH('dbo.InvestigationAssignments', 'DateAccepted') IS NULL
    ALTER TABLE dbo.InvestigationAssignments ADD DateAccepted datetime2 NULL;
IF COL_LENGTH('dbo.InvestigationAssignments', 'InspectionDueAt') IS NULL
    ALTER TABLE dbo.InvestigationAssignments ADD InspectionDueAt datetime2 NULL;
IF COL_LENGTH('dbo.InvestigationAssignments', 'InspectionCompletedAt') IS NULL
    ALTER TABLE dbo.InvestigationAssignments ADD InspectionCompletedAt datetime2 NULL;
IF COL_LENGTH('dbo.InvestigationAssignments', 'InspectionNotes') IS NULL
    ALTER TABLE dbo.InvestigationAssignments ADD InspectionNotes nvarchar(max) NULL;
IF COL_LENGTH('dbo.InvestigationAssignments', 'InspectionCompletedByUserID') IS NULL
    ALTER TABLE dbo.InvestigationAssignments ADD InspectionCompletedByUserID int NULL;
IF COL_LENGTH('dbo.InvestigationAssignments', 'AllocationReviewStatus') IS NULL
    ALTER TABLE dbo.InvestigationAssignments ADD AllocationReviewStatus nvarchar(50) NOT NULL
        CONSTRAINT DF_InvestigationAssignments_AllocationReviewStatus DEFAULT('Pending');
IF COL_LENGTH('dbo.InvestigationAssignments', 'AllocationReviewNotes') IS NULL
    ALTER TABLE dbo.InvestigationAssignments ADD AllocationReviewNotes nvarchar(max) NULL;
IF COL_LENGTH('dbo.InvestigationAssignments', 'AllocationReviewedAt') IS NULL
    ALTER TABLE dbo.InvestigationAssignments ADD AllocationReviewedAt datetime2 NULL;
IF COL_LENGTH('dbo.InvestigationAssignments', 'AllocationReviewedByUserID') IS NULL
    ALTER TABLE dbo.InvestigationAssignments ADD AllocationReviewedByUserID int NULL;

IF COL_LENGTH('dbo.InvestigationNotes', 'Status') IS NULL
    ALTER TABLE dbo.InvestigationNotes ADD Status nvarchar(50) NOT NULL
        CONSTRAINT DF_InvestigationNotes_Status DEFAULT('Active');

IF COL_LENGTH('dbo.Persons', 'IsArchived') IS NULL
    ALTER TABLE dbo.Persons ADD IsArchived bit NOT NULL
        CONSTRAINT DF_Persons_IsArchived DEFAULT(0);
IF COL_LENGTH('dbo.Persons', 'ArchivedAt') IS NULL
    ALTER TABLE dbo.Persons ADD ArchivedAt datetime2 NULL;
IF COL_LENGTH('dbo.Persons', 'ArchivedByUserID') IS NULL
    ALTER TABLE dbo.Persons ADD ArchivedByUserID int NULL;
IF COL_LENGTH('dbo.Persons', 'ArchiveReason') IS NULL
    ALTER TABLE dbo.Persons ADD ArchiveReason nvarchar(max) NULL;

IF OBJECT_ID(N'dbo.CourtAppearance', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CourtAppearance
    (
        CourtAppearanceID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CourtAppearance PRIMARY KEY,
        AppearanceDate datetime2 NOT NULL,
        CourtroomName nvarchar(256) NOT NULL CONSTRAINT DF_CourtAppearance_CourtroomName DEFAULT(''),
        PresidingOfficer nvarchar(256) NOT NULL CONSTRAINT DF_CourtAppearance_PresidingOfficer DEFAULT(''),
        AppearanceReason nvarchar(256) NOT NULL CONSTRAINT DF_CourtAppearance_AppearanceReason DEFAULT(''),
        Outcome nvarchar(256) NULL,
        Sentence nvarchar(2000) NULL,
        RemandDate datetime2 NULL,
        TypescriptLink nvarchar(500) NULL,
        Notes nvarchar(1000) NULL,
        FK_DocketID int NOT NULL
    );
    CREATE INDEX IX_CourtAppearance_FK_DocketID ON dbo.CourtAppearance(FK_DocketID);
END;

IF COL_LENGTH('dbo.CourtAppearance', 'Sentence') IS NULL
    ALTER TABLE dbo.CourtAppearance ADD Sentence nvarchar(2000) NULL;

IF COL_LENGTH('dbo.DocketPersonLinks', 'RelationshipRole') IS NULL
BEGIN
    ALTER TABLE dbo.DocketPersonLinks ADD RelationshipRole nvarchar(50) NOT NULL
        CONSTRAINT DF_DocketPersonLinks_RelationshipRole DEFAULT('Unspecified');
    EXEC(N'UPDATE links
              SET RelationshipRole = COALESCE(roles.RoleName, ''Unspecified'')
             FROM dbo.DocketPersonLinks links
             LEFT JOIN dbo.Persons persons ON persons.PersonID = links.FK_PersonID
             LEFT JOIN dbo.PersonRoles roles ON roles.PersonRoleID = persons.FK_PersonRoleID;');
END;
");

            await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.DocketStatusHistories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocketStatusHistories
    (
        HistoryID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocketStatusHistories PRIMARY KEY,
        FK_DocketID int NOT NULL,
        FromStatus nvarchar(50) NULL,
        ToStatus nvarchar(50) NULL,
        ChangedAt datetime2 NOT NULL CONSTRAINT DF_DocketStatusHistories_ChangedAt DEFAULT(SYSUTCDATETIME()),
        ChangedByUserID int NULL,
        Reason nvarchar(max) NULL
    );
    CREATE INDEX IX_DocketStatusHistories_FK_DocketID ON dbo.DocketStatusHistories(FK_DocketID);
END;

IF OBJECT_ID(N'dbo.EvidenceTransfers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EvidenceTransfers
    (
        TransferID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EvidenceTransfers PRIMARY KEY,
        FK_EvidenceID int NOT NULL,
        FromLocationID int NOT NULL,
        ToLocationID int NOT NULL,
        TransferredAt datetime2 NOT NULL CONSTRAINT DF_EvidenceTransfers_TransferredAt DEFAULT(SYSUTCDATETIME()),
        TransferredByUserID int NOT NULL,
        ReceivingOfficerUserID int NOT NULL,
        SignatureMethod nvarchar(50) NULL,
        PayloadHash nvarchar(256) NULL,
        TransferReason nvarchar(500) NULL
    );
    CREATE INDEX IX_EvidenceTransfers_FK_EvidenceID ON dbo.EvidenceTransfers(FK_EvidenceID);
END;

IF OBJECT_ID(N'dbo.ProsecutorBundles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProsecutorBundles
    (
        BundleID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProsecutorBundles PRIMARY KEY,
        FK_DocketID int NOT NULL,
        Title nvarchar(256) NULL,
        Notes nvarchar(max) NULL,
        Status nvarchar(50) NOT NULL CONSTRAINT DF_ProsecutorBundles_Status DEFAULT('Draft'),
        AISummaryText nvarchar(max) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_ProsecutorBundles_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CreatedByUserID int NOT NULL,
        UpdatedAt datetime2 NULL,
        UpdatedByUserID int NULL,
        ExportedAt datetime2 NULL,
        ExportedByUserID int NULL,
        ManifestPath nvarchar(max) NULL,
        Version int NOT NULL CONSTRAINT DF_ProsecutorBundles_Version DEFAULT(1),
        RecipientProsecutorUserID int NULL,
        SharedAt datetime2 NULL,
        SharedByUserID int NULL,
        ShareNote nvarchar(500) NULL,
        SharedManifestHash nvarchar(128) NULL
    );
    CREATE INDEX IX_ProsecutorBundles_FK_DocketID ON dbo.ProsecutorBundles(FK_DocketID);
END;

IF COL_LENGTH('dbo.ProsecutorBundles', 'Version') IS NULL
    ALTER TABLE dbo.ProsecutorBundles ADD Version int NOT NULL CONSTRAINT DF_ProsecutorBundles_Version DEFAULT(1);
IF COL_LENGTH('dbo.ProsecutorBundles', 'RecipientProsecutorUserID') IS NULL
    ALTER TABLE dbo.ProsecutorBundles ADD RecipientProsecutorUserID int NULL;
IF COL_LENGTH('dbo.ProsecutorBundles', 'SharedAt') IS NULL
    ALTER TABLE dbo.ProsecutorBundles ADD SharedAt datetime2 NULL;
IF COL_LENGTH('dbo.ProsecutorBundles', 'SharedByUserID') IS NULL
    ALTER TABLE dbo.ProsecutorBundles ADD SharedByUserID int NULL;
IF COL_LENGTH('dbo.ProsecutorBundles', 'ShareNote') IS NULL
    ALTER TABLE dbo.ProsecutorBundles ADD ShareNote nvarchar(500) NULL;
IF COL_LENGTH('dbo.ProsecutorBundles', 'SharedManifestHash') IS NULL
    ALTER TABLE dbo.ProsecutorBundles ADD SharedManifestHash nvarchar(128) NULL;

IF OBJECT_ID(N'dbo.CK_ProsecutorBundles_Status_Allowed', N'C') IS NOT NULL
    ALTER TABLE dbo.ProsecutorBundles DROP CONSTRAINT CK_ProsecutorBundles_Status_Allowed;
ALTER TABLE dbo.ProsecutorBundles WITH CHECK ADD CONSTRAINT CK_ProsecutorBundles_Status_Allowed
    CHECK ([Status] IN (N'Draft', N'Shared', N'Exported'));

IF OBJECT_ID(N'dbo.ProsecutorBundleItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProsecutorBundleItems
    (
        ItemID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProsecutorBundleItems PRIMARY KEY,
        FK_BundleID int NOT NULL,
        Section nvarchar(50) NULL,
        [Order] int NOT NULL,
        Name nvarchar(256) NULL,
        IsPrivate bit NOT NULL CONSTRAINT DF_ProsecutorBundleItems_IsPrivate DEFAULT(0),
        PrivacyReason nvarchar(max) NULL
    );
    CREATE INDEX IX_ProsecutorBundleItems_FK_BundleID ON dbo.ProsecutorBundleItems(FK_BundleID);
END;

IF OBJECT_ID(N'dbo.ProsecutorBundleRevisions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProsecutorBundleRevisions
    (
        RevisionID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProsecutorBundleRevisions PRIMARY KEY,
        FK_BundleID int NOT NULL,
        Version int NOT NULL,
        Title nvarchar(256) NULL,
        Notes nvarchar(max) NULL,
        AISummaryText nvarchar(max) NULL,
        Status nvarchar(50) NOT NULL CONSTRAINT DF_ProsecutorBundleRevisions_Status DEFAULT('Draft'),
        ManifestJson nvarchar(max) NOT NULL CONSTRAINT DF_ProsecutorBundleRevisions_ManifestJson DEFAULT('[]'),
        Reason nvarchar(max) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_ProsecutorBundleRevisions_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CreatedByUserID int NOT NULL
    );
    CREATE UNIQUE INDEX IX_ProsecutorBundleRevisions_Bundle_Version
        ON dbo.ProsecutorBundleRevisions(FK_BundleID, Version);
END;

IF OBJECT_ID(N'dbo.CK_ProsecutorBundleRevisions_Status_Allowed', N'C') IS NOT NULL
    ALTER TABLE dbo.ProsecutorBundleRevisions DROP CONSTRAINT CK_ProsecutorBundleRevisions_Status_Allowed;
ALTER TABLE dbo.ProsecutorBundleRevisions WITH CHECK ADD CONSTRAINT CK_ProsecutorBundleRevisions_Status_Allowed
    CHECK ([Status] IN (N'Draft', N'Shared', N'Exported'));

IF OBJECT_ID(N'dbo.PrivacyFlagHistories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PrivacyFlagHistories
    (
        PrivacyFlagHistoryID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PrivacyFlagHistories PRIMARY KEY,
        FK_DocketID int NOT NULL,
        IsPrivate bit NOT NULL,
        Reason nvarchar(max) NULL,
        Timestamp datetime2 NOT NULL CONSTRAINT DF_PrivacyFlagHistories_Timestamp DEFAULT(SYSUTCDATETIME()),
        ChangedByUserID int NOT NULL
    );
    CREATE INDEX IX_PrivacyFlagHistories_FK_DocketID ON dbo.PrivacyFlagHistories(FK_DocketID);
END;

IF OBJECT_ID(N'dbo.AIInteractionLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AIInteractionLogs
    (
        AIInteractionLogID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AIInteractionLogs PRIMARY KEY,
        FK_DocketID int NOT NULL,
        ActionType nvarchar(50) NULL,
        Payload nvarchar(max) NULL,
        Timestamp datetime2 NOT NULL CONSTRAINT DF_AIInteractionLogs_Timestamp DEFAULT(SYSUTCDATETIME()),
        PerformedByUserID int NOT NULL
    );
    CREATE INDEX IX_AIInteractionLogs_FK_DocketID ON dbo.AIInteractionLogs(FK_DocketID);
END;

IF OBJECT_ID(N'dbo.AIExtractions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AIExtractions
    (
        AIExtractionID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AIExtractions PRIMARY KEY,
        FK_DocketID int NOT NULL,
        ExtractedData nvarchar(max) NULL,
        IsVerified bit NOT NULL CONSTRAINT DF_AIExtractions_IsVerified DEFAULT(0),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_AIExtractions_CreatedAt DEFAULT(SYSUTCDATETIME())
    );
    CREATE INDEX IX_AIExtractions_FK_DocketID ON dbo.AIExtractions(FK_DocketID);
END;

IF OBJECT_ID(N'dbo.AISuggestions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AISuggestions
    (
        AISuggestionID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AISuggestions PRIMARY KEY,
        FK_DocketID int NOT NULL,
        SuggestionType nvarchar(max) NULL,
        Content nvarchar(max) NULL,
        Score float NOT NULL,
        Timestamp datetime2 NOT NULL CONSTRAINT DF_AISuggestions_Timestamp DEFAULT(SYSUTCDATETIME())
    );
    CREATE INDEX IX_AISuggestions_FK_DocketID ON dbo.AISuggestions(FK_DocketID);
END;

IF OBJECT_ID(N'dbo.CourtOutcomes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CourtOutcomes
    (
        CourtOutcomeID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CourtOutcomes PRIMARY KEY,
        FK_DocketID int NOT NULL,
        Outcome nvarchar(50) NULL,
        OutcomeDate datetime2 NOT NULL,
        Sentence nvarchar(max) NULL
    );
    CREATE INDEX IX_CourtOutcomes_FK_DocketID ON dbo.CourtOutcomes(FK_DocketID);
END;

IF OBJECT_ID(N'dbo.PersonArchives', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PersonArchives
    (
        PersonArchiveID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PersonArchives PRIMARY KEY,
        FK_PersonID int NOT NULL,
        Reason nvarchar(max) NULL,
        ArchivedByUserID int NOT NULL,
        ArchivedAt datetime2 NOT NULL CONSTRAINT DF_PersonArchives_ArchivedAt DEFAULT(SYSUTCDATETIME())
    );
    CREATE INDEX IX_PersonArchives_FK_PersonID ON dbo.PersonArchives(FK_PersonID);
END;

IF OBJECT_ID(N'dbo.UserRoleHistories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRoleHistories
    (
        UserRoleHistoryID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserRoleHistories PRIMARY KEY,
        FK_UserID int NOT NULL,
        OldRoleID int NOT NULL,
        NewRoleID int NOT NULL,
        OldRoleName nvarchar(100) NULL,
        NewRoleName nvarchar(100) NULL,
        Reason nvarchar(max) NULL,
        ChangedAt datetime2 NOT NULL CONSTRAINT DF_UserRoleHistories_ChangedAt DEFAULT(SYSUTCDATETIME()),
        ChangedByUserID int NOT NULL
    );
    CREATE INDEX IX_UserRoleHistories_FK_UserID ON dbo.UserRoleHistories(FK_UserID);
END;

IF OBJECT_ID(N'dbo.UserPasswordHistories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserPasswordHistories
    (
        UserPasswordHistoryID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserPasswordHistories PRIMARY KEY,
        FK_UserID int NOT NULL,
        PasswordHash nvarchar(256) NOT NULL,
        ChangeType nvarchar(50) NOT NULL CONSTRAINT DF_UserPasswordHistories_ChangeType DEFAULT('PasswordChange'),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_UserPasswordHistories_CreatedAt DEFAULT(SYSUTCDATETIME())
    );
    CREATE INDEX IX_UserPasswordHistories_FK_UserID_CreatedAt
        ON dbo.UserPasswordHistories(FK_UserID, CreatedAt DESC);
END;

IF OBJECT_ID(N'dbo.Faqs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Faqs
    (
        FaqID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Faqs PRIMARY KEY,
        Question nvarchar(250) NOT NULL,
        Answer nvarchar(max) NOT NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_Faqs_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CreatedByUserID int NOT NULL,
        UpdatedAt datetime2 NULL,
        UpdatedByUserID int NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_Faqs_IsDeleted DEFAULT(0),
        DeletedAt datetime2 NULL,
        DeletedByUserID int NULL,
        IsPublished bit NOT NULL CONSTRAINT DF_Faqs_IsPublished DEFAULT(0),
        PublishedAt datetime2 NULL,
        PublishedByUserID int NULL
    );
END;

IF COL_LENGTH('dbo.Faqs', 'IsPublished') IS NULL
    ALTER TABLE dbo.Faqs ADD IsPublished bit NOT NULL CONSTRAINT DF_Faqs_IsPublished DEFAULT(1);
IF COL_LENGTH('dbo.Faqs', 'PublishedAt') IS NULL
    ALTER TABLE dbo.Faqs ADD PublishedAt datetime2 NULL;
IF COL_LENGTH('dbo.Faqs', 'PublishedByUserID') IS NULL
    ALTER TABLE dbo.Faqs ADD PublishedByUserID int NULL;

IF OBJECT_ID(N'dbo.InvestigationNoteArchiveRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InvestigationNoteArchiveRequests
    (
        ArchiveRequestID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvestigationNoteArchiveRequests PRIMARY KEY,
        FK_NoteID int NOT NULL,
        Reason nvarchar(max) NOT NULL,
        Status nvarchar(50) NOT NULL CONSTRAINT DF_InvestigationNoteArchiveRequests_Status DEFAULT('Pending'),
        RequestedAt datetime2 NOT NULL CONSTRAINT DF_InvestigationNoteArchiveRequests_RequestedAt DEFAULT(SYSUTCDATETIME()),
        RequestedByUserID int NOT NULL,
        ReviewedAt datetime2 NULL,
        ReviewedByUserID int NULL,
        ReviewNotes nvarchar(max) NULL
    );
    CREATE INDEX IX_InvestigationNoteArchiveRequests_FK_NoteID ON dbo.InvestigationNoteArchiveRequests(FK_NoteID);
END;

IF OBJECT_ID(N'dbo.Feedback', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Feedback
    (
        FeedbackID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Feedback PRIMARY KEY,
        FK_UserID int NOT NULL,
        Subject nvarchar(150) NOT NULL,
        Message nvarchar(2000) NOT NULL,
        Response nvarchar(2000) NULL,
        Status nvarchar(30) NOT NULL CONSTRAINT DF_Feedback_Status DEFAULT('Submitted'),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_Feedback_CreatedAt DEFAULT(SYSUTCDATETIME()),
        RespondedAt datetime2 NULL,
        RespondedByUserID int NULL
    );
    CREATE INDEX IX_Feedback_FK_UserID ON dbo.Feedback(FK_UserID);
END;
");

            await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.SystemSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemSettings
    (
        SettingID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SystemSettings PRIMARY KEY,
        SettingKey nvarchar(100) NOT NULL,
        SettingValue nvarchar(500) NOT NULL,
        Description nvarchar(500) NOT NULL CONSTRAINT DF_SystemSettings_Description DEFAULT(''),
        DataType nvarchar(30) NOT NULL CONSTRAINT DF_SystemSettings_DataType DEFAULT('int'),
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_SystemSettings_UpdatedAt DEFAULT(SYSUTCDATETIME()),
        UpdatedByUserID int NULL
    );
    CREATE UNIQUE INDEX IX_SystemSettings_SettingKey ON dbo.SystemSettings(SettingKey);
END;
");

            await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.BackupRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BackupRecords
    (
        BackupID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_BackupRecords PRIMARY KEY,
        FileName nvarchar(260) NOT NULL,
        FilePath nvarchar(500) NOT NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_BackupRecords_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CreatedByUserID int NOT NULL,
        SizeBytes bigint NOT NULL CONSTRAINT DF_BackupRecords_SizeBytes DEFAULT(0),
        Status nvarchar(50) NOT NULL CONSTRAINT DF_BackupRecords_Status DEFAULT('Complete'),
        RestoredAt datetime2 NULL,
        RestoredByUserID int NULL,
        Notes nvarchar(max) NULL
    );
END;
");

            await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.SmsMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsMessages
    (
        SmsMessageID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsMessages PRIMARY KEY,
        FK_UserID int NULL,
        RecipientNumber nvarchar(30) NOT NULL CONSTRAINT DF_SmsMessages_RecipientNumber DEFAULT(''),
        Message nvarchar(max) NOT NULL,
        Purpose nvarchar(50) NOT NULL CONSTRAINT DF_SmsMessages_Purpose DEFAULT('Notification'),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_SmsMessages_CreatedAt DEFAULT(SYSUTCDATETIME()),
        SentAt datetime2 NULL,
        Status nvarchar(50) NOT NULL CONSTRAINT DF_SmsMessages_Status DEFAULT('Generated'),
        ProviderReference nvarchar(100) NULL
    );
END;

IF OBJECT_ID(N'dbo.AIInteractionLogs', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Dockets', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AIInteractionLogs_Dockets_FK_DocketID')
   AND NOT EXISTS
       (SELECT 1 FROM dbo.AIInteractionLogs logEntry
         LEFT JOIN dbo.Dockets docket ON docket.DocketID = logEntry.FK_DocketID
        WHERE docket.DocketID IS NULL)
    ALTER TABLE dbo.AIInteractionLogs WITH CHECK
        ADD CONSTRAINT FK_AIInteractionLogs_Dockets_FK_DocketID
        FOREIGN KEY (FK_DocketID) REFERENCES dbo.Dockets(DocketID);

IF OBJECT_ID(N'dbo.SmsMessages', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SmsMessages_Users_FK_UserID')
   AND NOT EXISTS
       (SELECT 1 FROM dbo.SmsMessages sms
         LEFT JOIN dbo.Users appUser ON appUser.UserID = sms.FK_UserID
        WHERE sms.FK_UserID IS NOT NULL AND appUser.UserID IS NULL)
    ALTER TABLE dbo.SmsMessages WITH CHECK
        ADD CONSTRAINT FK_SmsMessages_Users_FK_UserID
        FOREIGN KEY (FK_UserID) REFERENCES dbo.Users(UserID) ON DELETE SET NULL;

IF OBJECT_ID(N'dbo.Feedback', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Feedback_Users_FK_UserID')
   AND NOT EXISTS
       (SELECT 1 FROM dbo.Feedback feedback
         LEFT JOIN dbo.Users appUser ON appUser.UserID = feedback.FK_UserID
        WHERE appUser.UserID IS NULL)
    ALTER TABLE dbo.Feedback WITH CHECK
        ADD CONSTRAINT FK_Feedback_Users_FK_UserID
        FOREIGN KEY (FK_UserID) REFERENCES dbo.Users(UserID);

IF OBJECT_ID(N'dbo.UserAuditLogs', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.UserAuditLogs') AND name = N'IX_UserAuditLogs_Timestamp')
    CREATE INDEX IX_UserAuditLogs_Timestamp ON dbo.UserAuditLogs([Timestamp] DESC);

IF OBJECT_ID(N'dbo.UserAuditLogs', N'U') IS NOT NULL
    EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_UserAuditLogs_Immutable
             ON dbo.UserAuditLogs
             AFTER UPDATE, DELETE
             AS
             BEGIN
                 SET NOCOUNT ON;
                 THROW 51000, ''SAPS DDMS audit records are immutable.'', 1;
             END;');
");

            await SeedSettingAsync(db, "SESSION_TIMEOUT_MINUTES", "60", "Inactive session timeout used when creating login sessions.", "int");
            await SeedSettingAsync(db, "OTP_EXPIRY_MINUTES", "5", "Minutes before a login one-time PIN expires.", "int");
            await SeedSettingAsync(db, "PASSWORD_RESET_EXPIRY_HOURS", "1", "Hours before a password reset link expires.", "int");
            await SeedSettingAsync(db, "PASSWORD_HISTORY_COUNT", "5", "Number of recent password hashes rejected during password creation, change, and reset.", "int");
            await SeedSettingAsync(db, "INSPECTION_SLA_HOURS", "24", "Hours allowed for initial docket inspection after assignment acceptance.", "int");
            await SeedSettingAsync(db, "MAX_LOGIN_ATTEMPTS", "3", "Failed password attempts before an account is locked.", "int");
            await SeedSettingAsync(db, "DOCKET_PRIORITY_HIGH_DAYS", "7", "Business rule for flagging high priority docket age.", "int");
            await SeedSettingAsync(db, "EVIDENCE_RETENTION_YEARS", "7", "Business rule for evidence retention.", "int");
            await SeedSettingAsync(db, "ARCHIVE_ELIGIBLE_DAYS", "90", "Days after closure before a docket is eligible for archive review.", "int");
            await SeedSettingAsync(db, "INSPECTION_REMINDER_HOURS", "4", "Hours before inspection SLA when reminders should be raised.", "int");
            await SeedSettingAsync(db, "MAX_ACTIVE_DOCKETS_PER_OFFICER", "10", "Maximum number of active dockets that may be allocated to a single Investigating Officer.", "int");
            await SeedSettingAsync(db, "TWO_FACTOR_REQUIRED", "false", "When true, every login requires a one-time PIN after password validation.", "bool");

            foreach (var incidentType in OperationalLookupCatalog.DefaultIncidentTypes)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM dbo.IncidentTypes WHERE TypeName = {incidentType.TypeName})
    INSERT INTO dbo.IncidentTypes (TypeName, Description)
    VALUES ({incidentType.TypeName}, {incidentType.Description});
");
            }

            await db.Database.ExecuteSqlRawAsync(@"
UPDATE dbo.SystemSettings
   SET SettingValue = '3', UpdatedAt = SYSUTCDATETIME()
 WHERE SettingKey = 'MAX_LOGIN_ATTEMPTS'
   AND SettingValue = '5'
   AND UpdatedByUserID IS NULL;
");
        }

        public static async Task EnsureOperationalReferenceDataAsync(ApplicationDbContext db)
        {
            await SeedSettingAsync(db, "SESSION_TIMEOUT_MINUTES", "60", "Inactive session timeout used when creating login sessions.", "int");
            await SeedSettingAsync(db, "OTP_EXPIRY_MINUTES", "5", "Minutes before a login one-time PIN expires.", "int");
            await SeedSettingAsync(db, "PASSWORD_RESET_EXPIRY_HOURS", "1", "Hours before a password reset link expires.", "int");
            await SeedSettingAsync(db, "PASSWORD_HISTORY_COUNT", "5", "Number of recent password hashes rejected during password creation, change, and reset.", "int");
            await SeedSettingAsync(db, "INSPECTION_SLA_HOURS", "24", "Hours allowed for initial docket inspection after assignment acceptance.", "int");
            await SeedSettingAsync(db, "MAX_LOGIN_ATTEMPTS", "3", "Failed password attempts before an account is locked.", "int");
            await SeedSettingAsync(db, "DOCKET_PRIORITY_HIGH_DAYS", "7", "Business rule for flagging high priority docket age.", "int");
            await SeedSettingAsync(db, "EVIDENCE_RETENTION_YEARS", "7", "Business rule for evidence retention.", "int");
            await SeedSettingAsync(db, "ARCHIVE_ELIGIBLE_DAYS", "90", "Days after closure before a docket is eligible for archive review.", "int");
            await SeedSettingAsync(db, "INSPECTION_REMINDER_HOURS", "4", "Hours before inspection SLA when reminders should be raised.", "int");
            await SeedSettingAsync(db, "MAX_ACTIVE_DOCKETS_PER_OFFICER", "10", "Maximum number of active dockets that may be allocated to a single Investigating Officer.", "int");
            await SeedSettingAsync(db, "TWO_FACTOR_REQUIRED", "false", "When true, every login requires a one-time PIN after password validation.", "bool");

            foreach (var incidentType in OperationalLookupCatalog.DefaultIncidentTypes)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM dbo.IncidentTypes WHERE TypeName = {incidentType.TypeName})
    INSERT INTO dbo.IncidentTypes (TypeName, Description)
    VALUES ({incidentType.TypeName}, {incidentType.Description});
");
            }

            foreach (var statusName in new[]
                     {
                         "OPEN",
                         "ALLOCATED",
                         "UNDER_INVESTIGATION",
                         "PENDING_PROSECUTOR",
                         "CLOSED",
                         "ARCHIVED",
                         "COURT_OUTCOME_RECORDED",
                         "REOPENED"
                     })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM dbo.DocketStatuses WHERE StatusName = {statusName})
    INSERT INTO dbo.DocketStatuses (StatusName)
    VALUES ({statusName});
");
            }

            foreach (var statusName in new[] { "OPEN", "IN_PROGRESS", "COMPLETED", "SUSPENDED" })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM dbo.InvestigationStatuses WHERE StatusName = {statusName})
    INSERT INTO dbo.InvestigationStatuses (StatusName)
    VALUES ({statusName});
");
            }

            foreach (var evidenceType in new[] { "Physical", "Digital", "Document", "Biological", "Fingerprint", "Photographic", "Audio", "Video" })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM dbo.EvidenceTypes WHERE Name = {evidenceType})
    INSERT INTO dbo.EvidenceTypes (Name)
    VALUES ({evidenceType});
");
            }

            foreach (var location in new[]
                     {
                         new { Name = "Evidence Room - Main", Address = "SAPS HQ", Storage = "Climate controlled" },
                         new { Name = "Evidence Room - Secondary", Address = "SAPS Annex", Storage = "Standard" },
                         new { Name = "Forensics Lab", Address = "Forensics Division", Storage = "Controlled access" },
                         new { Name = "Digital Evidence Lab", Address = "Cyber Unit", Storage = "Static-safe" },
                         new { Name = "With IO", Address = "In custody of IO", Storage = "Officer responsible" },
                         new { Name = "Court", Address = "Submitted to court", Storage = "Court registry" }
                     })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM dbo.EvidenceLocations WHERE LocationName = {location.Name})
    INSERT INTO dbo.EvidenceLocations (LocationName, PhysicalAddress, StorageRequirements)
    VALUES ({location.Name}, {location.Address}, {location.Storage});
");
            }

            foreach (var role in new[]
                     {
                         new { RoleName = "Complainant", Code = "CMP", Description = "Person who reports the crime" },
                         new { RoleName = "Suspect", Code = "SUS", Description = "Person accused of the crime" },
                         new { RoleName = "Witness", Code = "WIT", Description = "Person who witnessed the incident" },
                         new { RoleName = "Victim", Code = "VIC", Description = "Person who suffered harm" }
                     })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM dbo.PersonRoles WHERE RoleName = {role.RoleName})
    INSERT INTO dbo.PersonRoles (RoleName, Code, IsActive, Description)
    VALUES ({role.RoleName}, {role.Code}, 1, {role.Description});
");
            }
        }

        private static async Task SeedSettingAsync(ApplicationDbContext db, string key, string value, string description, string dataType)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = {key})
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType, UpdatedAt)
    VALUES ({key}, {value}, {description}, {dataType}, SYSUTCDATETIME());
");
        }
    }
}
