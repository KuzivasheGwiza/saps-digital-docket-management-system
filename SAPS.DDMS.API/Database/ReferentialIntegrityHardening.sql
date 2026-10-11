/*
    SAPS DDMS referential-integrity hardening
    Integrated with the API deployment assets for reviewed DBA execution.

    Safety and deployment notes
    ---------------------------
    - This script is intentionally idempotent and does not repair, delete, or
      rewrite application data.
    - Every target foreign key is preflighted before any foreign-key DDL runs.
      If even one orphan is present, the script stops before changing the schema.
    - CHECK constraints are applied only when existing rows satisfy the rule.
      A dirty rule is reported and skipped; existing data is never coerced.
    - Existing equivalent foreign keys and existing named CHECK constraints are
      re-enabled and re-trusted with WITH CHECK.
    - All relationships use NO ACTION for updates and deletes. Cascading deletes
      would be unsafe for docket, evidence, bundle, person, and user history data.
    - Have a DBA review and run this in a maintenance window. This file is not
      invoked automatically by application startup.
*/

USE [SAPS.DDMS];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'tempdb..#RequiredForeignKeys', N'U') IS NOT NULL
    DROP TABLE #RequiredForeignKeys;

CREATE TABLE #RequiredForeignKeys
(
    ConstraintName sysname NOT NULL,
    ChildSchema sysname NOT NULL,
    ChildTable sysname NOT NULL,
    ChildColumn sysname NOT NULL,
    ParentSchema sysname NOT NULL,
    ParentTable sysname NOT NULL,
    ParentColumn sysname NOT NULL,
    IndexName sysname NOT NULL,
    CONSTRAINT PK_RequiredForeignKeys PRIMARY KEY (ConstraintName)
);

INSERT INTO #RequiredForeignKeys
(
    ConstraintName,
    ChildSchema,
    ChildTable,
    ChildColumn,
    ParentSchema,
    ParentTable,
    ParentColumn,
    IndexName
)
VALUES
    (N'FK_AIExtractions_Dockets_FK_DocketID', N'dbo', N'AIExtractions', N'FK_DocketID', N'dbo', N'Dockets', N'DocketID', N'IX_RI_AIExtractions_FK_DocketID'),
    (N'FK_AISuggestions_Dockets_FK_DocketID', N'dbo', N'AISuggestions', N'FK_DocketID', N'dbo', N'Dockets', N'DocketID', N'IX_RI_AISuggestions_FK_DocketID'),
    (N'FK_CourtAppearance_Dockets_FK_DocketID', N'dbo', N'CourtAppearance', N'FK_DocketID', N'dbo', N'Dockets', N'DocketID', N'IX_RI_CourtAppearance_FK_DocketID'),
    (N'FK_CourtOutcomes_Dockets_FK_DocketID', N'dbo', N'CourtOutcomes', N'FK_DocketID', N'dbo', N'Dockets', N'DocketID', N'IX_RI_CourtOutcomes_FK_DocketID'),
    (N'FK_DocketStatusHistories_Dockets_FK_DocketID', N'dbo', N'DocketStatusHistories', N'FK_DocketID', N'dbo', N'Dockets', N'DocketID', N'IX_RI_DocketStatusHistories_FK_DocketID'),
    (N'FK_EvidenceTransfers_Evidences_FK_EvidenceID', N'dbo', N'EvidenceTransfers', N'FK_EvidenceID', N'dbo', N'Evidences', N'EvidenceID', N'IX_RI_EvidenceTransfers_FK_EvidenceID'),
    (N'FK_InvestigationNoteArchiveRequests_InvestigationNotes_FK_NoteID', N'dbo', N'InvestigationNoteArchiveRequests', N'FK_NoteID', N'dbo', N'InvestigationNotes', N'NoteID', N'IX_RI_InvestigationNoteArchiveRequests_FK_NoteID'),
    (N'FK_PersonArchives_Persons_FK_PersonID', N'dbo', N'PersonArchives', N'FK_PersonID', N'dbo', N'Persons', N'PersonID', N'IX_RI_PersonArchives_FK_PersonID'),
    (N'FK_PrivacyFlagHistories_Dockets_FK_DocketID', N'dbo', N'PrivacyFlagHistories', N'FK_DocketID', N'dbo', N'Dockets', N'DocketID', N'IX_RI_PrivacyFlagHistories_FK_DocketID'),
    (N'FK_ProsecutorBundleItems_ProsecutorBundles_FK_BundleID', N'dbo', N'ProsecutorBundleItems', N'FK_BundleID', N'dbo', N'ProsecutorBundles', N'BundleID', N'IX_RI_ProsecutorBundleItems_FK_BundleID'),
    (N'FK_ProsecutorBundleRevisions_ProsecutorBundles_FK_BundleID', N'dbo', N'ProsecutorBundleRevisions', N'FK_BundleID', N'dbo', N'ProsecutorBundles', N'BundleID', N'IX_RI_ProsecutorBundleRevisions_FK_BundleID'),
    (N'FK_ProsecutorBundles_Dockets_FK_DocketID', N'dbo', N'ProsecutorBundles', N'FK_DocketID', N'dbo', N'Dockets', N'DocketID', N'IX_RI_ProsecutorBundles_FK_DocketID'),
    (N'FK_UserPasswordHistories_Users_FK_UserID', N'dbo', N'UserPasswordHistories', N'FK_UserID', N'dbo', N'Users', N'UserID', N'IX_RI_UserPasswordHistories_FK_UserID'),
    (N'FK_UserRoleHistories_Users_FK_UserID', N'dbo', N'UserRoleHistories', N'FK_UserID', N'dbo', N'Users', N'UserID', N'IX_RI_UserRoleHistories_FK_UserID'),
    (N'FK_AIInteractionLogs_Users_PerformedByUserID', N'dbo', N'AIInteractionLogs', N'PerformedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_AIInteractionLogs_PerformedByUserID'),
    (N'FK_BackupRecords_Users_CreatedByUserID', N'dbo', N'BackupRecords', N'CreatedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_BackupRecords_CreatedByUserID'),
    (N'FK_BackupRecords_Users_RestoredByUserID', N'dbo', N'BackupRecords', N'RestoredByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_BackupRecords_RestoredByUserID'),
    (N'FK_DocketStatusHistories_Users_ChangedByUserID', N'dbo', N'DocketStatusHistories', N'ChangedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_DocketStatusHistories_ChangedByUserID'),
    (N'FK_EvidenceTransfers_EvidenceLocations_FromLocationID', N'dbo', N'EvidenceTransfers', N'FromLocationID', N'dbo', N'EvidenceLocations', N'EvidenceLocationID', N'IX_RI_EvidenceTransfers_FromLocationID'),
    (N'FK_EvidenceTransfers_EvidenceLocations_ToLocationID', N'dbo', N'EvidenceTransfers', N'ToLocationID', N'dbo', N'EvidenceLocations', N'EvidenceLocationID', N'IX_RI_EvidenceTransfers_ToLocationID'),
    (N'FK_EvidenceTransfers_Users_TransferredByUserID', N'dbo', N'EvidenceTransfers', N'TransferredByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_EvidenceTransfers_TransferredByUserID'),
    (N'FK_EvidenceTransfers_Users_ReceivingOfficerUserID', N'dbo', N'EvidenceTransfers', N'ReceivingOfficerUserID', N'dbo', N'Users', N'UserID', N'IX_RI_EvidenceTransfers_ReceivingOfficerUserID'),
    (N'FK_Faqs_Users_CreatedByUserID', N'dbo', N'Faqs', N'CreatedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_Faqs_CreatedByUserID'),
    (N'FK_Faqs_Users_UpdatedByUserID', N'dbo', N'Faqs', N'UpdatedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_Faqs_UpdatedByUserID'),
    (N'FK_Faqs_Users_DeletedByUserID', N'dbo', N'Faqs', N'DeletedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_Faqs_DeletedByUserID'),
    (N'FK_Faqs_Users_PublishedByUserID', N'dbo', N'Faqs', N'PublishedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_Faqs_PublishedByUserID'),
    (N'FK_Feedback_Users_RespondedByUserID', N'dbo', N'Feedback', N'RespondedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_Feedback_RespondedByUserID'),
    (N'FK_InvestigationAssignments_Users_InspectionCompletedByUserID', N'dbo', N'InvestigationAssignments', N'InspectionCompletedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_InvestigationAssignments_InspectionCompletedByUserID'),
    (N'FK_InvestigationAssignments_Users_AllocationReviewedByUserID', N'dbo', N'InvestigationAssignments', N'AllocationReviewedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_InvestigationAssignments_AllocationReviewedByUserID'),
    (N'FK_InvestigationNoteArchiveRequests_Users_RequestedByUserID', N'dbo', N'InvestigationNoteArchiveRequests', N'RequestedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_InvestigationNoteArchiveRequests_RequestedByUserID'),
    (N'FK_InvestigationNoteArchiveRequests_Users_ReviewedByUserID', N'dbo', N'InvestigationNoteArchiveRequests', N'ReviewedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_InvestigationNoteArchiveRequests_ReviewedByUserID'),
    (N'FK_PersonArchives_Users_ArchivedByUserID', N'dbo', N'PersonArchives', N'ArchivedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_PersonArchives_ArchivedByUserID'),
    (N'FK_Persons_Users_ArchivedByUserID', N'dbo', N'Persons', N'ArchivedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_Persons_ArchivedByUserID'),
    (N'FK_PrivacyFlagHistories_Users_ChangedByUserID', N'dbo', N'PrivacyFlagHistories', N'ChangedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_PrivacyFlagHistories_ChangedByUserID'),
    (N'FK_ProsecutorBundleRevisions_Users_CreatedByUserID', N'dbo', N'ProsecutorBundleRevisions', N'CreatedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_ProsecutorBundleRevisions_CreatedByUserID'),
    (N'FK_ProsecutorBundles_Users_CreatedByUserID', N'dbo', N'ProsecutorBundles', N'CreatedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_ProsecutorBundles_CreatedByUserID'),
    (N'FK_ProsecutorBundles_Users_UpdatedByUserID', N'dbo', N'ProsecutorBundles', N'UpdatedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_ProsecutorBundles_UpdatedByUserID'),
    (N'FK_ProsecutorBundles_Users_ExportedByUserID', N'dbo', N'ProsecutorBundles', N'ExportedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_ProsecutorBundles_ExportedByUserID'),
    (N'FK_SystemSettings_Users_UpdatedByUserID', N'dbo', N'SystemSettings', N'UpdatedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_SystemSettings_UpdatedByUserID'),
    (N'FK_UserRoleHistories_Roles_OldRoleID', N'dbo', N'UserRoleHistories', N'OldRoleID', N'dbo', N'Roles', N'RoleID', N'IX_RI_UserRoleHistories_OldRoleID'),
    (N'FK_UserRoleHistories_Roles_NewRoleID', N'dbo', N'UserRoleHistories', N'NewRoleID', N'dbo', N'Roles', N'RoleID', N'IX_RI_UserRoleHistories_NewRoleID'),
    (N'FK_UserRoleHistories_Users_ChangedByUserID', N'dbo', N'UserRoleHistories', N'ChangedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_UserRoleHistories_ChangedByUserID'),
    (N'FK_Users_Users_CreatedByUserID', N'dbo', N'Users', N'CreatedByUserID', N'dbo', N'Users', N'UserID', N'IX_RI_Users_CreatedByUserID');

/*
    Preflight all declared relationships first. The separate pass guarantees that an
    orphan in a later table cannot leave an earlier table modified.
*/
DECLARE
    @ConstraintName sysname,
    @ChildSchema sysname,
    @ChildTable sysname,
    @ChildColumn sysname,
    @ParentSchema sysname,
    @ParentTable sysname,
    @ParentColumn sysname,
    @IndexName sysname,
    @ChildObjectId int,
    @ParentObjectId int,
    @HasOrphans bit,
    @Sql nvarchar(max),
    @Message nvarchar(2048);

DECLARE ForeignKeyPreflight CURSOR LOCAL FAST_FORWARD FOR
    SELECT
        ConstraintName,
        ChildSchema,
        ChildTable,
        ChildColumn,
        ParentSchema,
        ParentTable,
        ParentColumn,
        IndexName
    FROM #RequiredForeignKeys
    ORDER BY ConstraintName;

OPEN ForeignKeyPreflight;

FETCH NEXT FROM ForeignKeyPreflight INTO
    @ConstraintName,
    @ChildSchema,
    @ChildTable,
    @ChildColumn,
    @ParentSchema,
    @ParentTable,
    @ParentColumn,
    @IndexName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @ChildObjectId = OBJECT_ID(QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable), N'U');
    SET @ParentObjectId = OBJECT_ID(QUOTENAME(@ParentSchema) + N'.' + QUOTENAME(@ParentTable), N'U');

    IF @ChildObjectId IS NULL
    BEGIN
        SET @Message = N'Referential-integrity preflight failed: child table '
            + QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable) + N' does not exist.';
        THROW 51000, @Message, 1;
    END;

    IF @ParentObjectId IS NULL
    BEGIN
        SET @Message = N'Referential-integrity preflight failed: parent table '
            + QUOTENAME(@ParentSchema) + N'.' + QUOTENAME(@ParentTable) + N' does not exist.';
        THROW 51001, @Message, 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = @ChildObjectId
          AND name = @ChildColumn
    )
    BEGIN
        SET @Message = N'Referential-integrity preflight failed: child column '
            + QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable) + N'.' + QUOTENAME(@ChildColumn)
            + N' does not exist.';
        THROW 51002, @Message, 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = @ParentObjectId
          AND name = @ParentColumn
    )
    BEGIN
        SET @Message = N'Referential-integrity preflight failed: parent column '
            + QUOTENAME(@ParentSchema) + N'.' + QUOTENAME(@ParentTable) + N'.' + QUOTENAME(@ParentColumn)
            + N' does not exist.';
        THROW 51003, @Message, 1;
    END;

    SET @HasOrphans = 0;
    SET @Sql = N'
        SELECT @Result = CONVERT(bit, CASE WHEN EXISTS
        (
            SELECT 1
            FROM ' + QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable) + N' AS child_row
            LEFT JOIN ' + QUOTENAME(@ParentSchema) + N'.' + QUOTENAME(@ParentTable) + N' AS parent_row
              ON parent_row.' + QUOTENAME(@ParentColumn) + N' = child_row.' + QUOTENAME(@ChildColumn) + N'
            WHERE child_row.' + QUOTENAME(@ChildColumn) + N' IS NOT NULL
              AND parent_row.' + QUOTENAME(@ParentColumn) + N' IS NULL
        ) THEN 1 ELSE 0 END);';

    EXEC sys.sp_executesql
        @Sql,
        N'@Result bit OUTPUT',
        @Result = @HasOrphans OUTPUT;

    IF @HasOrphans = 1
    BEGIN
        SET @Message = N'Referential-integrity preflight failed: orphan rows exist for '
            + QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable) + N'.' + QUOTENAME(@ChildColumn)
            + N' -> ' + QUOTENAME(@ParentSchema) + N'.' + QUOTENAME(@ParentTable) + N'.' + QUOTENAME(@ParentColumn)
            + N'. No foreign-key changes were made.';
        THROW 51004, @Message, 1;
    END;

    FETCH NEXT FROM ForeignKeyPreflight INTO
        @ConstraintName,
        @ChildSchema,
        @ChildTable,
        @ChildColumn,
        @ParentSchema,
        @ParentTable,
        @ParentColumn,
        @IndexName;
END;

CLOSE ForeignKeyPreflight;
DEALLOCATE ForeignKeyPreflight;

/* Apply or re-trust the verified foreign keys and ensure a supporting index. */
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE
        @ExistingForeignKeyObjectId int,
        @ExistingForeignKeyName sysname;

    DECLARE ForeignKeyApply CURSOR LOCAL FAST_FORWARD FOR
        SELECT
            ConstraintName,
            ChildSchema,
            ChildTable,
            ChildColumn,
            ParentSchema,
            ParentTable,
            ParentColumn,
            IndexName
        FROM #RequiredForeignKeys
        ORDER BY ConstraintName;

    OPEN ForeignKeyApply;

    FETCH NEXT FROM ForeignKeyApply INTO
        @ConstraintName,
        @ChildSchema,
        @ChildTable,
        @ChildColumn,
        @ParentSchema,
        @ParentTable,
        @ParentColumn,
        @IndexName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @ChildObjectId = OBJECT_ID(QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable), N'U');
        SET @ParentObjectId = OBJECT_ID(QUOTENAME(@ParentSchema) + N'.' + QUOTENAME(@ParentTable), N'U');
        SET @ExistingForeignKeyObjectId = NULL;
        SET @ExistingForeignKeyName = NULL;

        SELECT TOP (1)
            @ExistingForeignKeyObjectId = foreign_key.object_id,
            @ExistingForeignKeyName = foreign_key.name
        FROM sys.foreign_keys AS foreign_key
        INNER JOIN sys.foreign_key_columns AS foreign_key_column
            ON foreign_key_column.constraint_object_id = foreign_key.object_id
        INNER JOIN sys.columns AS child_column
            ON child_column.object_id = foreign_key_column.parent_object_id
           AND child_column.column_id = foreign_key_column.parent_column_id
        INNER JOIN sys.columns AS parent_column
            ON parent_column.object_id = foreign_key_column.referenced_object_id
           AND parent_column.column_id = foreign_key_column.referenced_column_id
        WHERE foreign_key.parent_object_id = @ChildObjectId
          AND foreign_key.referenced_object_id = @ParentObjectId
          AND child_column.name = @ChildColumn
          AND parent_column.name = @ParentColumn
          AND 1 =
          (
              SELECT COUNT(*)
              FROM sys.foreign_key_columns AS component
              WHERE component.constraint_object_id = foreign_key.object_id
          )
        ORDER BY foreign_key.object_id;

        IF @ExistingForeignKeyObjectId IS NULL
        BEGIN
            IF EXISTS
            (
                SELECT 1
                FROM sys.objects
                WHERE schema_id = SCHEMA_ID(@ChildSchema)
                  AND name = @ConstraintName
            )
            BEGIN
                SET @Message = N'Cannot add ' + QUOTENAME(@ConstraintName)
                    + N': an unrelated object already uses that name in schema ' + QUOTENAME(@ChildSchema) + N'.';
                THROW 51005, @Message, 1;
            END;

            SET @Sql = N'ALTER TABLE '
                + QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable)
                + N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@ConstraintName)
                + N' FOREIGN KEY (' + QUOTENAME(@ChildColumn) + N') REFERENCES '
                + QUOTENAME(@ParentSchema) + N'.' + QUOTENAME(@ParentTable)
                + N' (' + QUOTENAME(@ParentColumn) + N')'
                + N' ON UPDATE NO ACTION ON DELETE NO ACTION;'
                + N' ALTER TABLE ' + QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable)
                + N' CHECK CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
            EXEC sys.sp_executesql @Sql;
            PRINT N'ADDED foreign key ' + QUOTENAME(@ConstraintName) + N'.';
        END
        ELSE
        BEGIN
            SET @Sql = N'ALTER TABLE '
                + QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable)
                + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(@ExistingForeignKeyName) + N';';
            EXEC sys.sp_executesql @Sql;
            PRINT N'VERIFIED foreign key ' + QUOTENAME(@ExistingForeignKeyName) + N'.';
        END;

        IF NOT EXISTS
        (
            SELECT 1
            FROM sys.indexes AS table_index
            INNER JOIN sys.index_columns AS index_column
                ON index_column.object_id = table_index.object_id
               AND index_column.index_id = table_index.index_id
            INNER JOIN sys.columns AS indexed_column
                ON indexed_column.object_id = index_column.object_id
               AND indexed_column.column_id = index_column.column_id
            WHERE table_index.object_id = @ChildObjectId
              AND table_index.type IN (1, 2)
              AND table_index.is_disabled = 0
              AND table_index.is_hypothetical = 0
              AND index_column.key_ordinal = 1
              AND indexed_column.name = @ChildColumn
        )
        BEGIN
            IF EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = @ChildObjectId
                  AND name = @IndexName
            )
            BEGIN
                SET @Message = N'Cannot add supporting index ' + QUOTENAME(@IndexName)
                    + N' on ' + QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable)
                    + N': that table already has a different index with the same name.';
                THROW 51006, @Message, 1;
            END;

            SET @Sql = N'CREATE NONCLUSTERED INDEX ' + QUOTENAME(@IndexName)
                + N' ON ' + QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable)
                + N' (' + QUOTENAME(@ChildColumn) + N');';
            EXEC sys.sp_executesql @Sql;
            PRINT N'ADDED supporting index ' + QUOTENAME(@IndexName) + N'.';
        END;

        FETCH NEXT FROM ForeignKeyApply INTO
            @ConstraintName,
            @ChildSchema,
            @ChildTable,
            @ChildColumn,
            @ParentSchema,
            @ParentTable,
            @ParentColumn,
            @IndexName;
    END;

    CLOSE ForeignKeyApply;
    DEALLOCATE ForeignKeyApply;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/*
    Important bounded-domain and lifecycle invariants.

    These rules deliberately avoid volatile expressions such as GETUTCDATE().
    Each rule is checked against existing rows and skipped when legacy data is
    incompatible, leaving a precise remediation trail in the deployment output.
*/
IF OBJECT_ID(N'tempdb..#CheckRules', N'U') IS NOT NULL
    DROP TABLE #CheckRules;

CREATE TABLE #CheckRules
(
    ConstraintName sysname NOT NULL,
    SchemaName sysname NOT NULL,
    TableName sysname NOT NULL,
    PredicateSql nvarchar(max) NOT NULL,
    RuleDescription nvarchar(400) NOT NULL,
    CONSTRAINT PK_CheckRules PRIMARY KEY (ConstraintName)
);

IF OBJECT_ID(N'tempdb..#CheckRuleColumns', N'U') IS NOT NULL
    DROP TABLE #CheckRuleColumns;

CREATE TABLE #CheckRuleColumns
(
    ConstraintName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    CONSTRAINT PK_CheckRuleColumns PRIMARY KEY (ConstraintName, ColumnName)
);

INSERT INTO #CheckRules
(
    ConstraintName,
    SchemaName,
    TableName,
    PredicateSql,
    RuleDescription
)
VALUES
    (N'CK_Evidences_Quantity_Positive', N'dbo', N'Evidences', N'[Quantity] >= (1)', N'Evidence quantity must be at least one.'),
    (N'CK_Evidences_SignatureMethod_Allowed', N'dbo', N'Evidences', N'[SignatureMethod] IS NULL OR [SignatureMethod] IN (N''Ink'', N''Stylus'', N''Smartcard'')', N'Evidence signatures use one of the supported capture methods.'),
    (N'CK_EvidenceTransfers_SignatureMethod_Allowed', N'dbo', N'EvidenceTransfers', N'[SignatureMethod] IS NULL OR [SignatureMethod] IN (N''Ink'', N''Stylus'', N''Smartcard'')', N'Transfer signatures use one of the supported capture methods.'),
    (N'CK_AISuggestions_Score_Range', N'dbo', N'AISuggestions', N'[Score] >= (0) AND [Score] <= (1)', N'AI similarity score is normalized to the inclusive range zero through one.'),
    (N'CK_ProsecutorBundleItems_Order_Positive', N'dbo', N'ProsecutorBundleItems', N'[Order] >= (1)', N'Bundle manifest ordering starts at one.'),
    (N'CK_ProsecutorBundles_Version_Positive', N'dbo', N'ProsecutorBundles', N'[Version] >= (1)', N'Bundle versions start at one.'),
    (N'CK_ProsecutorBundleRevisions_Version_Positive', N'dbo', N'ProsecutorBundleRevisions', N'[Version] >= (1)', N'Bundle revision versions start at one.'),
    (N'CK_ProsecutorBundles_Status_Allowed', N'dbo', N'ProsecutorBundles', N'[Status] IN (N''Draft'', N''Shared'', N''Exported'')', N'Bundle status matches the implemented workflow while retaining legacy exports.'),
    (N'CK_ProsecutorBundleRevisions_Status_Allowed', N'dbo', N'ProsecutorBundleRevisions', N'[Status] IN (N''Draft'', N''Shared'', N''Exported'')', N'Bundle revision status matches the implemented workflow while retaining legacy exports.'),
    (N'CK_InvestigationNoteArchiveRequests_Status_Allowed', N'dbo', N'InvestigationNoteArchiveRequests', N'[Status] IN (N''Pending'', N''Approved'', N''Rejected'')', N'Archive requests use the implemented review states.'),
    (N'CK_Users_FailedLoginAttempts_Nonnegative', N'dbo', N'Users', N'[FailedLoginAttempts] >= (0)', N'Failed login attempts cannot be negative.'),
    (N'CK_BackupRecords_SizeBytes_Nonnegative', N'dbo', N'BackupRecords', N'[SizeBytes] >= (0)', N'Backup size cannot be negative.'),
    (N'CK_Dockets_LifecycleDates', N'dbo', N'Dockets', N'([DateAllocated] IS NULL OR [DateAllocated] >= [DateCreated]) AND ([DocketArchivedAt] IS NULL OR [DocketArchivedAt] >= [DateCreated]) AND ([DocketClosedAt] IS NULL OR [DocketClosedAt] >= [DateCreated]) AND ([ReopenedAt] IS NULL OR [ReopenedAt] >= [DateCreated])', N'Docket lifecycle timestamps cannot precede docket creation.'),
    (N'CK_ProsecutorBundles_LifecycleDates', N'dbo', N'ProsecutorBundles', N'([UpdatedAt] IS NULL OR [UpdatedAt] >= [CreatedAt]) AND ([ExportedAt] IS NULL OR [ExportedAt] >= [CreatedAt])', N'Bundle updates and exports cannot precede bundle creation.'),
    (N'CK_BackupRecords_RestoredAt', N'dbo', N'BackupRecords', N'[RestoredAt] IS NULL OR [RestoredAt] >= [CreatedAt]', N'A restore timestamp cannot precede backup creation.'),
    (N'CK_Sessions_LifecycleDates', N'dbo', N'Sessions', N'[ExpiryTime] >= [Login] AND ([LastActivity] IS NULL OR [LastActivity] >= [Login]) AND ([Logout] IS NULL OR [Logout] >= [Login])', N'Session expiry, activity, and logout cannot precede login.'),
    (N'CK_InvestigationNoteArchiveRequests_ReviewedAt', N'dbo', N'InvestigationNoteArchiveRequests', N'[ReviewedAt] IS NULL OR [ReviewedAt] >= [RequestedAt]', N'Archive review cannot precede the request.');

INSERT INTO #CheckRuleColumns (ConstraintName, ColumnName)
VALUES
    (N'CK_Evidences_Quantity_Positive', N'Quantity'),
    (N'CK_Evidences_SignatureMethod_Allowed', N'SignatureMethod'),
    (N'CK_EvidenceTransfers_SignatureMethod_Allowed', N'SignatureMethod'),
    (N'CK_AISuggestions_Score_Range', N'Score'),
    (N'CK_ProsecutorBundleItems_Order_Positive', N'Order'),
    (N'CK_ProsecutorBundles_Version_Positive', N'Version'),
    (N'CK_ProsecutorBundleRevisions_Version_Positive', N'Version'),
    (N'CK_ProsecutorBundles_Status_Allowed', N'Status'),
    (N'CK_ProsecutorBundleRevisions_Status_Allowed', N'Status'),
    (N'CK_InvestigationNoteArchiveRequests_Status_Allowed', N'Status'),
    (N'CK_Users_FailedLoginAttempts_Nonnegative', N'FailedLoginAttempts'),
    (N'CK_BackupRecords_SizeBytes_Nonnegative', N'SizeBytes'),
    (N'CK_Dockets_LifecycleDates', N'DateCreated'),
    (N'CK_Dockets_LifecycleDates', N'DateAllocated'),
    (N'CK_Dockets_LifecycleDates', N'DocketArchivedAt'),
    (N'CK_Dockets_LifecycleDates', N'DocketClosedAt'),
    (N'CK_Dockets_LifecycleDates', N'ReopenedAt'),
    (N'CK_ProsecutorBundles_LifecycleDates', N'CreatedAt'),
    (N'CK_ProsecutorBundles_LifecycleDates', N'UpdatedAt'),
    (N'CK_ProsecutorBundles_LifecycleDates', N'ExportedAt'),
    (N'CK_BackupRecords_RestoredAt', N'CreatedAt'),
    (N'CK_BackupRecords_RestoredAt', N'RestoredAt'),
    (N'CK_Sessions_LifecycleDates', N'Login'),
    (N'CK_Sessions_LifecycleDates', N'ExpiryTime'),
    (N'CK_Sessions_LifecycleDates', N'LastActivity'),
    (N'CK_Sessions_LifecycleDates', N'Logout'),
    (N'CK_InvestigationNoteArchiveRequests_ReviewedAt', N'RequestedAt'),
    (N'CK_InvestigationNoteArchiveRequests_ReviewedAt', N'ReviewedAt');

DECLARE
    @CheckConstraintName sysname,
    @CheckSchema sysname,
    @CheckTable sysname,
    @PredicateSql nvarchar(max),
    @RuleDescription nvarchar(400),
    @CheckObjectId int,
    @ExistingCheckObjectId int,
    @HasViolations bit,
    @MissingColumn sysname;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE CheckRuleApply CURSOR LOCAL FAST_FORWARD FOR
        SELECT
            ConstraintName,
            SchemaName,
            TableName,
            PredicateSql,
            RuleDescription
        FROM #CheckRules
        ORDER BY ConstraintName;

    OPEN CheckRuleApply;

    FETCH NEXT FROM CheckRuleApply INTO
        @CheckConstraintName,
        @CheckSchema,
        @CheckTable,
        @PredicateSql,
        @RuleDescription;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @CheckObjectId = OBJECT_ID(QUOTENAME(@CheckSchema) + N'.' + QUOTENAME(@CheckTable), N'U');

        IF @CheckObjectId IS NULL
        BEGIN
            PRINT N'SKIPPED check ' + QUOTENAME(@CheckConstraintName) + N': table '
                + QUOTENAME(@CheckSchema) + N'.' + QUOTENAME(@CheckTable) + N' does not exist.';
        END
        ELSE
        BEGIN
            SET @MissingColumn = NULL;

            SELECT TOP (1)
                @MissingColumn = required_column.ColumnName
            FROM #CheckRuleColumns AS required_column
            WHERE required_column.ConstraintName = @CheckConstraintName
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM sys.columns AS actual_column
                  WHERE actual_column.object_id = @CheckObjectId
                    AND actual_column.name = required_column.ColumnName
              )
            ORDER BY required_column.ColumnName;

            IF @MissingColumn IS NOT NULL
            BEGIN
                PRINT N'SKIPPED check ' + QUOTENAME(@CheckConstraintName)
                    + N': required column ' + QUOTENAME(@MissingColumn) + N' does not exist on '
                    + QUOTENAME(@CheckSchema) + N'.' + QUOTENAME(@CheckTable) + N'.';
            END
            ELSE
            BEGIN
                SET @HasViolations = 0;
                SET @Sql = N'SELECT @Result = CONVERT(bit, CASE WHEN EXISTS '
                    + N'(SELECT 1 FROM ' + QUOTENAME(@CheckSchema) + N'.' + QUOTENAME(@CheckTable)
                    + N' WHERE NOT (' + @PredicateSql + N')) THEN 1 ELSE 0 END);';

                EXEC sys.sp_executesql
                    @Sql,
                    N'@Result bit OUTPUT',
                    @Result = @HasViolations OUTPUT;

                IF @HasViolations = 1
                BEGIN
                    PRINT N'SKIPPED check ' + QUOTENAME(@CheckConstraintName)
                        + N': existing data violates the rule (' + @RuleDescription + N').';
                END
                ELSE
                BEGIN
                    SET @ExistingCheckObjectId = OBJECT_ID(
                        QUOTENAME(@CheckSchema) + N'.' + QUOTENAME(@CheckConstraintName),
                        N'C'
                    );

                    IF @ExistingCheckObjectId IS NULL
                    BEGIN
                        IF EXISTS
                        (
                            SELECT 1
                            FROM sys.objects
                            WHERE schema_id = SCHEMA_ID(@CheckSchema)
                              AND name = @CheckConstraintName
                        )
                        BEGIN
                            SET @Message = N'Cannot add ' + QUOTENAME(@CheckConstraintName)
                                + N': an unrelated object already uses that name in schema '
                                + QUOTENAME(@CheckSchema) + N'.';
                            THROW 51007, @Message, 1;
                        END;

                        SET @Sql = N'ALTER TABLE '
                            + QUOTENAME(@CheckSchema) + N'.' + QUOTENAME(@CheckTable)
                            + N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@CheckConstraintName)
                            + N' CHECK (' + @PredicateSql + N');'
                            + N' ALTER TABLE ' + QUOTENAME(@CheckSchema) + N'.' + QUOTENAME(@CheckTable)
                            + N' CHECK CONSTRAINT ' + QUOTENAME(@CheckConstraintName) + N';';
                        EXEC sys.sp_executesql @Sql;
                        PRINT N'ADDED check ' + QUOTENAME(@CheckConstraintName) + N'.';
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS
                        (
                            SELECT 1
                            FROM sys.check_constraints
                            WHERE object_id = @ExistingCheckObjectId
                              AND parent_object_id = @CheckObjectId
                        )
                        BEGIN
                            SET @Message = N'Named check ' + QUOTENAME(@CheckConstraintName)
                                + N' belongs to a different table; the expected invariant was not applied.';
                            THROW 51008, @Message, 1;
                        END;

                        SET @Sql = N'ALTER TABLE '
                            + QUOTENAME(@CheckSchema) + N'.' + QUOTENAME(@CheckTable)
                            + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(@CheckConstraintName) + N';';
                        EXEC sys.sp_executesql @Sql;
                        PRINT N'VERIFIED check ' + QUOTENAME(@CheckConstraintName) + N'.';
                    END;
                END;
            END;
        END;

        FETCH NEXT FROM CheckRuleApply INTO
            @CheckConstraintName,
            @CheckSchema,
            @CheckTable,
            @PredicateSql,
            @RuleDescription;
    END;

    CLOSE CheckRuleApply;
    DEALLOCATE CheckRuleApply;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* Deployment evidence: every row should end in PRESENT/TRUSTED after a clean run. */
SELECT
    target.ConstraintName AS ExpectedConstraintName,
    QUOTENAME(target.ChildSchema) + N'.' + QUOTENAME(target.ChildTable) + N'.' + QUOTENAME(target.ChildColumn) AS ChildReference,
    QUOTENAME(target.ParentSchema) + N'.' + QUOTENAME(target.ParentTable) + N'.' + QUOTENAME(target.ParentColumn) AS ParentReference,
    actual.ForeignKeyName,
    actual.IsDisabled,
    actual.IsNotTrusted,
    CASE
        WHEN actual.ForeignKeyName IS NULL THEN N'MISSING'
        WHEN actual.IsDisabled = 0 AND actual.IsNotTrusted = 0 THEN N'PRESENT/TRUSTED'
        ELSE N'PRESENT/UNTRUSTED'
    END AS VerificationStatus
FROM #RequiredForeignKeys AS target
OUTER APPLY
(
    SELECT TOP (1)
        foreign_key.name AS ForeignKeyName,
        foreign_key.is_disabled AS IsDisabled,
        foreign_key.is_not_trusted AS IsNotTrusted
    FROM sys.foreign_keys AS foreign_key
    INNER JOIN sys.foreign_key_columns AS foreign_key_column
        ON foreign_key_column.constraint_object_id = foreign_key.object_id
    INNER JOIN sys.columns AS child_column
        ON child_column.object_id = foreign_key_column.parent_object_id
       AND child_column.column_id = foreign_key_column.parent_column_id
    INNER JOIN sys.columns AS parent_column
        ON parent_column.object_id = foreign_key_column.referenced_object_id
       AND parent_column.column_id = foreign_key_column.referenced_column_id
    WHERE foreign_key.parent_object_id = OBJECT_ID(QUOTENAME(target.ChildSchema) + N'.' + QUOTENAME(target.ChildTable), N'U')
      AND foreign_key.referenced_object_id = OBJECT_ID(QUOTENAME(target.ParentSchema) + N'.' + QUOTENAME(target.ParentTable), N'U')
      AND child_column.name = target.ChildColumn
      AND parent_column.name = target.ParentColumn
      AND 1 =
      (
          SELECT COUNT(*)
          FROM sys.foreign_key_columns AS component
          WHERE component.constraint_object_id = foreign_key.object_id
      )
    ORDER BY foreign_key.object_id
) AS actual
ORDER BY target.ConstraintName;

SELECT
    target.ConstraintName AS ExpectedConstraintName,
    QUOTENAME(target.SchemaName) + N'.' + QUOTENAME(target.TableName) AS TargetTable,
    target.RuleDescription,
    target.PredicateSql AS ExpectedPredicate,
    actual.name AS CheckConstraintName,
    actual.definition AS InstalledDefinition,
    actual.is_disabled AS IsDisabled,
    actual.is_not_trusted AS IsNotTrusted,
    CASE
        WHEN target_table.object_id IS NULL THEN N'TABLE MISSING'
        WHEN actual.object_id IS NULL THEN N'NOT APPLIED (review deployment messages)'
        WHEN actual.is_disabled = 0 AND actual.is_not_trusted = 0 THEN N'PRESENT/TRUSTED'
        ELSE N'PRESENT/UNTRUSTED'
    END AS VerificationStatus
FROM #CheckRules AS target
LEFT JOIN sys.schemas AS target_schema
    ON target_schema.name = target.SchemaName
LEFT JOIN sys.tables AS target_table
    ON target_table.schema_id = target_schema.schema_id
   AND target_table.name = target.TableName
LEFT JOIN sys.check_constraints AS actual
    ON actual.parent_object_id = target_table.object_id
   AND actual.name = target.ConstraintName
ORDER BY target.ConstraintName;

PRINT N'Referential-integrity hardening completed. Review any SKIPPED checks before production promotion.';
