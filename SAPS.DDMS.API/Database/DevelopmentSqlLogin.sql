/*
    Development-only SQL login for running the SAPS DDMS API locally.
    Production deployments must use a DBA-managed service account, rotated password,
    Encrypt=True, and a trusted SQL Server certificate.

    SQL Server Express must allow SQL authentication before this login can be used.
    Run this script in SQLCMD mode and provide DDMS_SQL_PASSWORD, for example:
    sqlcmd -S .\SQLEXPRESS -E -v DDMS_SQL_PASSWORD="use-a-private-password" -i DevelopmentSqlLogin.sql

    Never commit the supplied password or reuse it outside local development.
*/

USE master;
GO

EXEC xp_instance_regwrite
     N'HKEY_LOCAL_MACHINE',
     N'Software\Microsoft\MSSQLServer\MSSQLServer',
     N'LoginMode',
     REG_DWORD,
     2;
GO

IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = 'saps_ddms_app')
BEGIN
    DECLARE @Password nvarchar(128) = N'$(DDMS_SQL_PASSWORD)';

    IF @Password = N'$(DDMS_SQL_PASSWORD)' OR LEN(@Password) < 16
        THROW 50000, 'Provide DDMS_SQL_PASSWORD with at least 16 characters in SQLCMD mode.', 1;

    DECLARE @CreateLoginSql nvarchar(max) =
        N'CREATE LOGIN [saps_ddms_app] WITH PASSWORD = '
        + QUOTENAME(@Password, '''')
        + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';

    EXEC sys.sp_executesql @CreateLoginSql;
END;
GO

ALTER LOGIN [saps_ddms_app] ENABLE;
GO

USE [SAPS.DDMS];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'saps_ddms_app')
    CREATE USER [saps_ddms_app] FOR LOGIN [saps_ddms_app];
GO

ALTER ROLE db_owner ADD MEMBER [saps_ddms_app];
GO
