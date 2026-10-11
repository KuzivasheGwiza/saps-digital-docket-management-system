using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SAPS.DDMS.API.Data;

namespace SAPS.DDMS.API.Services;

public sealed record DatabaseBackupResult(
    string FileName,
    string FilePath,
    long SizeBytes,
    int TableCount,
    int RowCount,
    int UploadFileCount);

public sealed record DatabaseRestoreResult(
    int TableCount,
    int RowCount,
    int UploadFileCount);

public interface IDatabaseBackupService
{
    Task<DatabaseBackupResult> CreateBackupAsync(int userId, CancellationToken cancellationToken = default);
    Task<DatabaseRestoreResult> RestoreBackupAsync(string backupPackagePath, CancellationToken cancellationToken = default);
}

public sealed class DatabaseBackupService(
    ApplicationDbContext db,
    IWebHostEnvironment environment,
    ILogger<DatabaseBackupService> logger)
    : IDatabaseBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _connectionString = db.Database.GetConnectionString()
        ?? throw new InvalidOperationException("The SAPS DDMS database connection string is not configured.");

    public async Task<DatabaseBackupResult> CreateBackupAsync(int userId, CancellationToken cancellationToken = default)
    {
        var webRoot = EnsureWebRoot();
        var backupRoot = Path.Combine(webRoot, "backups");
        Directory.CreateDirectory(backupRoot);

        var fileName = $"SAPS_DDMS_full_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        var filePath = Path.Combine(backupRoot, fileName);
        var tables = GetTableDescriptors();
        var uploads = GetUploadFiles(webRoot);
        var dataSet = new DataSet("SapsDdmsOperationalSnapshot");
        var manifest = new BackupManifest
        {
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            UploadFileCount = uploads.Count
        };

        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            foreach (var table in tables)
            {
                var dataTable = await ReadTableAsync(connection, table, cancellationToken);
                dataSet.Tables.Add(dataTable);
                manifest.Tables.Add(new BackupTableManifest
                {
                    DatasetName = table.DatasetName,
                    Schema = table.Schema,
                    Table = table.Table,
                    RowCount = dataTable.Rows.Count
                });
            }
        }

        using (var archive = ZipFile.Open(filePath, ZipArchiveMode.Create))
        {
            var databaseEntry = archive.CreateEntry("database.xml", CompressionLevel.Optimal);
            await using (var stream = databaseEntry.Open())
            {
                dataSet.WriteXml(stream, XmlWriteMode.WriteSchema);
            }

            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using (var stream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
            }

            foreach (var uploadFile in uploads)
            {
                var entryName = uploadFile.Replace('\\', '/');
                archive.CreateEntryFromFile(Path.Combine(webRoot, uploadFile), entryName, CompressionLevel.Optimal);
            }
        }

        var info = new FileInfo(filePath);
        return new DatabaseBackupResult(
            fileName,
            filePath,
            info.Length,
            manifest.Tables.Count,
            manifest.Tables.Sum(item => item.RowCount),
            uploads.Count);
    }

    public async Task<DatabaseRestoreResult> RestoreBackupAsync(string backupPackagePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupPackagePath))
            throw new FileNotFoundException("The selected backup package no longer exists on disk.", backupPackagePath);

        var webRoot = EnsureWebRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"saps-ddms-restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            BackupManifest manifest;
            var dataSet = new DataSet("SapsDdmsOperationalSnapshot");
            var stagedUploads = Path.Combine(tempRoot, "uploads");
            Directory.CreateDirectory(stagedUploads);

            using (var archive = ZipFile.OpenRead(backupPackagePath))
            {
                var manifestEntry = archive.GetEntry("manifest.json")
                    ?? throw new InvalidOperationException("The backup package is missing manifest.json.");
                await using (var manifestStream = manifestEntry.Open())
                {
                    manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream, JsonOptions, cancellationToken)
                        ?? throw new InvalidOperationException("The backup manifest could not be read.");
                }

                if (manifest.SchemaVersion != 2)
                    throw new InvalidOperationException("The backup package format is not supported by this version of SAPS DDMS.");

                var databaseEntry = archive.GetEntry("database.xml")
                    ?? throw new InvalidOperationException("The backup package is missing database.xml.");
                await using (var databaseStream = databaseEntry.Open())
                {
                    dataSet.ReadXml(databaseStream, XmlReadMode.ReadSchema);
                }

                foreach (var entry in archive.Entries.Where(item =>
                             !string.IsNullOrWhiteSpace(item.Name) &&
                             item.FullName.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)))
                {
                    var targetPath = Path.Combine(tempRoot, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    entry.ExtractToFile(targetPath, overwrite: true);
                }
            }

            var expectedTables = GetTableDescriptors();
            var expectedByDatasetName = expectedTables.ToDictionary(item => item.DatasetName, StringComparer.OrdinalIgnoreCase);
            var datasetTables = dataSet.Tables.Cast<DataTable>().Select(item => item.TableName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = expectedByDatasetName.Keys.Where(name => !datasetTables.Contains(name)).ToList();
            var unexpected = datasetTables.Where(name => !expectedByDatasetName.ContainsKey(name)).ToList();
            if (missing.Count > 0 || unexpected.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The backup package does not match the current operational schema. Missing: {string.Join(", ", missing)}. Unexpected: {string.Join(", ", unexpected)}.");
            }

            await RestoreTablesAsync(
                dataSet.Tables.Cast<DataTable>().Select(table => (Table: table, Descriptor: expectedByDatasetName[table.TableName])).ToList(),
                cancellationToken);

            ReplaceUploadsDirectory(
                currentUploadsPath: Path.Combine(webRoot, "uploads"),
                restoredUploadsPath: stagedUploads);

            return new DatabaseRestoreResult(
                expectedTables.Count,
                dataSet.Tables.Cast<DataTable>().Sum(item => item.Rows.Count),
                manifest.UploadFileCount);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Temporary restore folder {Folder} could not be removed.", tempRoot);
            }
        }
    }

    private async Task RestoreTablesAsync(
        IReadOnlyList<(DataTable Table, TableDescriptor Descriptor)> tables,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // The audit table is protected against UPDATE and DELETE. Suspend
            // that trigger only inside this restore transaction. SQL Server
            // rolls this DDL change back with the transaction if restore fails.
            await SetAuditLogImmutabilityTriggerAsync(
                connection,
                transaction,
                enabled: false,
                cancellationToken);

            foreach (var (_, descriptor) in tables)
                await ExecuteAsync(connection, transaction, $"ALTER TABLE {descriptor.FullName} NOCHECK CONSTRAINT ALL", cancellationToken);

            foreach (var (_, descriptor) in tables)
                await ExecuteAsync(connection, transaction, $"DELETE FROM {descriptor.FullName}", cancellationToken);

            foreach (var (table, descriptor) in tables)
            {
                await InsertRowsAsync(connection, transaction, table, descriptor, cancellationToken);
                await ReseedIdentityAsync(connection, transaction, table, descriptor, cancellationToken);
            }

            foreach (var (_, descriptor) in tables)
                await ExecuteAsync(connection, transaction, $"ALTER TABLE {descriptor.FullName} WITH CHECK CHECK CONSTRAINT ALL", cancellationToken);

            await SetAuditLogImmutabilityTriggerAsync(
                connection,
                transaction,
                enabled: true,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task SetAuditLogImmutabilityTriggerAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        bool enabled,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 120;
        command.CommandText = enabled
            ? "IF OBJECT_ID(N'[dbo].[TR_UserAuditLogs_Immutable]', N'TR') IS NOT NULL ENABLE TRIGGER [TR_UserAuditLogs_Immutable] ON [dbo].[UserAuditLogs]"
            : "IF OBJECT_ID(N'[dbo].[TR_UserAuditLogs_Immutable]', N'TR') IS NOT NULL DISABLE TRIGGER [TR_UserAuditLogs_Immutable] ON [dbo].[UserAuditLogs]";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertRowsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        DataTable table,
        TableDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        if (table.Rows.Count == 0)
            return;

        var identityColumn = await GetIdentityColumnAsync(connection, transaction, descriptor, cancellationToken);
        if (!string.IsNullOrWhiteSpace(identityColumn))
            await ExecuteAsync(connection, transaction, $"SET IDENTITY_INSERT {descriptor.FullName} ON", cancellationToken);

        try
        {
            var columns = table.Columns.Cast<DataColumn>().ToList();
            var quotedColumns = string.Join(", ", columns.Select(column => QuoteIdentifier(column.ColumnName)));
            var parameterNames = string.Join(", ", columns.Select((_, index) => $"@p{index}"));
            var insertSql = $"INSERT INTO {descriptor.FullName} ({quotedColumns}) VALUES ({parameterNames})";

            // SqlBulkCopy can wait indefinitely for a RESOURCE_SEMAPHORE
            // memory grant on SQL Server Express. Parameterised inserts are
            // slower but make restore reliable on constrained installations.
            foreach (DataRow row in table.Rows)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = insertSql;
                command.CommandTimeout = 120;

                for (var index = 0; index < columns.Count; index++)
                    command.Parameters.AddWithValue($"@p{index}", row[columns[index]] ?? DBNull.Value);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(identityColumn))
                await ExecuteAsync(connection, transaction, $"SET IDENTITY_INSERT {descriptor.FullName} OFF", CancellationToken.None);
        }
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private async Task ReseedIdentityAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        DataTable table,
        TableDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var identityColumn = await GetIdentityColumnAsync(connection, transaction, descriptor, cancellationToken);
        if (string.IsNullOrWhiteSpace(identityColumn))
            return;

        long reseedValue = 0;
        if (table.Rows.Count > 0)
        {
            reseedValue = table.AsEnumerable()
                .Select(row => row[identityColumn])
                .Where(value => value != DBNull.Value && value != null)
                .Select(value => Convert.ToInt64(value, CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0)
                .Max();
        }

        var sql = $"DBCC CHECKIDENT ('{descriptor.Schema}.{descriptor.Table}', RESEED, {reseedValue.ToString(CultureInfo.InvariantCulture)})";
        await ExecuteAsync(connection, transaction, sql, cancellationToken);
    }

    private async Task<string?> GetIdentityColumnAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        TableDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT TOP (1) c.name
            FROM sys.identity_columns c
            INNER JOIN sys.tables t ON t.object_id = c.object_id
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = @schema AND t.name = @table
            """;
        command.Parameters.AddWithValue("@schema", descriptor.Schema);
        command.Parameters.AddWithValue("@table", descriptor.Table);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString();
    }

    private async Task<DataTable> ReadTableAsync(SqlConnection connection, TableDescriptor descriptor, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {descriptor.FullName}";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable(descriptor.DatasetName);
        table.Load(reader);
        return table;
    }

    private async Task ExecuteAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private List<TableDescriptor> GetTableDescriptors()
    {
        return db.Model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned() &&
                                 entityType.GetTableName() is not null)
            .Select(entityType => new TableDescriptor(
                entityType.GetSchema() ?? "dbo",
                entityType.GetTableName()!,
                entityType.ClrType.Name))
            .DistinctBy(descriptor => descriptor.FullName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(descriptor => descriptor.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(descriptor => descriptor.Table, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> GetUploadFiles(string webRoot)
    {
        var uploadsPath = Path.Combine(webRoot, "uploads");
        if (!Directory.Exists(uploadsPath))
            return [];

        return Directory.GetFiles(uploadsPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(webRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string EnsureWebRoot()
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        Directory.CreateDirectory(webRoot);
        return webRoot;
    }

    private void ReplaceUploadsDirectory(string currentUploadsPath, string restoredUploadsPath)
    {
        var rollbackPath = Path.Combine(Path.GetTempPath(), $"saps-ddms-uploads-rollback-{Guid.NewGuid():N}");

        try
        {
            if (Directory.Exists(currentUploadsPath))
                Directory.Move(currentUploadsPath, rollbackPath);

            Directory.CreateDirectory(currentUploadsPath);
            if (Directory.Exists(restoredUploadsPath))
                CopyDirectory(restoredUploadsPath, currentUploadsPath);

            if (Directory.Exists(rollbackPath))
                Directory.Delete(rollbackPath, recursive: true);
        }
        catch
        {
            try
            {
                if (Directory.Exists(currentUploadsPath))
                    Directory.Delete(currentUploadsPath, recursive: true);
                if (Directory.Exists(rollbackPath))
                    Directory.Move(rollbackPath, currentUploadsPath);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The uploads directory rollback failed after a restore error.");
            }

            throw;
        }
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relative));
        }

        foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, file);
            var target = Path.Combine(destinationPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private sealed record TableDescriptor(string Schema, string Table, string EntityName)
    {
        public string FullName => $"[{Schema}].[{Table}]";
        public string DatasetName => $"{Schema}__{Table}";
    }

    private sealed class BackupManifest
    {
        public int SchemaVersion { get; set; } = 2;
        public string Product { get; set; } = "SAPS DDMS";
        public string Format { get; set; } = "full-zip-snapshot";
        public DateTime CreatedAtUtc { get; set; }
        public int CreatedByUserId { get; set; }
        public int UploadFileCount { get; set; }
        public List<BackupTableManifest> Tables { get; set; } = [];
    }

    private sealed class BackupTableManifest
    {
        public string DatasetName { get; set; } = string.Empty;
        public string Schema { get; set; } = "dbo";
        public string Table { get; set; } = string.Empty;
        public int RowCount { get; set; }
    }
}
