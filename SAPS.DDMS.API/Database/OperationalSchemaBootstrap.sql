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
GO

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
GO

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
GO

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
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = 'SESSION_TIMEOUT_MINUTES')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType) VALUES ('SESSION_TIMEOUT_MINUTES', '60', 'Inactive session timeout used when creating login sessions.', 'int');
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = 'OTP_EXPIRY_MINUTES')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType) VALUES ('OTP_EXPIRY_MINUTES', '5', 'Minutes before a login one-time PIN expires.', 'int');
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = 'PASSWORD_RESET_EXPIRY_HOURS')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType) VALUES ('PASSWORD_RESET_EXPIRY_HOURS', '1', 'Hours before a password reset link expires.', 'int');
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = 'INSPECTION_SLA_HOURS')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType) VALUES ('INSPECTION_SLA_HOURS', '24', 'Hours allowed for initial docket inspection after assignment acceptance.', 'int');
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = 'MAX_LOGIN_ATTEMPTS')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType) VALUES ('MAX_LOGIN_ATTEMPTS', '5', 'Failed password attempts before an account is locked.', 'int');
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = 'DOCKET_PRIORITY_HIGH_DAYS')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType) VALUES ('DOCKET_PRIORITY_HIGH_DAYS', '7', 'Business rule for flagging high priority docket age.', 'int');
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = 'EVIDENCE_RETENTION_YEARS')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType) VALUES ('EVIDENCE_RETENTION_YEARS', '7', 'Business rule for evidence retention.', 'int');
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = 'ARCHIVE_ELIGIBLE_DAYS')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType) VALUES ('ARCHIVE_ELIGIBLE_DAYS', '90', 'Days after closure before a docket is eligible for archive review.', 'int');
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = 'INSPECTION_REMINDER_HOURS')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType) VALUES ('INSPECTION_REMINDER_HOURS', '4', 'Hours before inspection SLA when reminders should be raised.', 'int');
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = 'TWO_FACTOR_REQUIRED')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, Description, DataType) VALUES ('TWO_FACTOR_REQUIRED', 'false', 'When true, every login requires a one-time PIN after password validation.', 'bool');
GO
