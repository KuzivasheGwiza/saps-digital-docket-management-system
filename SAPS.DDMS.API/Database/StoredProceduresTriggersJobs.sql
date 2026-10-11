/*
    SAPS DDMS database complexity objects.
    Apply this script from SQL Server Management Studio or sqlcmd after the database exists.
*/

CREATE OR ALTER PROCEDURE dbo.sp_DDMS_DocketStatusSummary
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ds.StatusName,
        COUNT(d.DocketID) AS DocketCount
    FROM dbo.DocketStatuses ds
    LEFT JOIN dbo.Dockets d ON d.FK_DocketStatusID = ds.DocketStatusID
    GROUP BY ds.StatusName
    ORDER BY DocketCount DESC, ds.StatusName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_DDMS_StationWorkload
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.Province,
        s.Region,
        s.StationName,
        ds.StatusName,
        COUNT(d.DocketID) AS DocketCount
    FROM dbo.Stations s
    LEFT JOIN dbo.Dockets d ON d.FK_StationID = s.StationID
    LEFT JOIN dbo.DocketStatuses ds ON ds.DocketStatusID = d.FK_DocketStatusID
    GROUP BY s.Province, s.Region, s.StationName, ds.StatusName
    ORDER BY s.Province, s.StationName, ds.StatusName;
END;
GO

CREATE OR ALTER TRIGGER dbo.tr_DDMS_DocketStatus_Audit
ON dbo.Dockets
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.DocketStatusHistories
    (
        FK_DocketID,
        FromStatus,
        ToStatus,
        ChangedAt,
        ChangedByUserID,
        Reason
    )
    SELECT
        i.DocketID,
        oldStatus.StatusName,
        newStatus.StatusName,
        SYSUTCDATETIME(),
        i.FK_CreatedByUserID,
        'Database trigger captured docket status update'
    FROM inserted i
    INNER JOIN deleted d ON d.DocketID = i.DocketID
    LEFT JOIN dbo.DocketStatuses oldStatus ON oldStatus.DocketStatusID = d.FK_DocketStatusID
    LEFT JOIN dbo.DocketStatuses newStatus ON newStatus.DocketStatusID = i.FK_DocketStatusID
    WHERE ISNULL(i.FK_DocketStatusID, -1) <> ISNULL(d.FK_DocketStatusID, -1);
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_DDMS_CreateBackupMarker
    @CreatedByUserID int
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.BackupRecords
    (
        FileName,
        FilePath,
        CreatedAt,
        CreatedByUserID,
        SizeBytes,
        Status,
        Notes
    )
    VALUES
    (
        CONCAT('SAPS_DDMS_sql_job_marker_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'), '.txt'),
        'SQL Agent marker - backup managed by DBA job',
        SYSUTCDATETIME(),
        @CreatedByUserID,
        0,
        'Scheduled Marker',
        'Marker written by scheduled SQL Agent-style backup procedure.'
    );
END;
GO
