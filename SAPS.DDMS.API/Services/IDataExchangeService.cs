using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Models.DTOs;

namespace SAPS.DDMS.API.Services
{
    public interface IDataExchangeService
    {
        Task<DataExchangeExportFile> ExportAsync(
            string dataset,
            string format,
            DataExchangeFilterDto filters,
            int userId,
            CancellationToken cancellationToken = default);

        Task<LookupImportPreviewDto> PreviewLookupImportAsync(
            LookupImportDocumentDto document,
            int userId,
            CancellationToken cancellationToken = default);

        Task<LookupImportApplyResultDto> ApplyLookupImportAsync(
            LookupImportDocumentDto document,
            int userId,
            CancellationToken cancellationToken = default);
    }

    public sealed record DataExchangeExportFile(
        byte[] Content,
        string ContentType,
        string FileName,
        string Dataset,
        string Format,
        int RowCount);

    public sealed class DataExchangeValidationException : Exception
    {
        public DataExchangeValidationException(string message) : base(message) { }
    }

    public sealed class DataExchangeService : IDataExchangeService
    {
        private const int MaxExportRows = 10_000;
        private const int MaxImportRows = 500;
        private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(10);

        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;
        private readonly IDataProtector _previewProtector;

        public DataExchangeService(
            ApplicationDbContext db,
            IAuditService audit,
            IDataProtectionProvider dataProtectionProvider)
        {
            _db = db;
            _audit = audit;
            _previewProtector = dataProtectionProvider.CreateProtector(
                "SAPS.DDMS.DataExchange.LookupImportPreview.v1");
        }

        public async Task<DataExchangeExportFile> ExportAsync(
            string dataset,
            string format,
            DataExchangeFilterDto filters,
            int userId,
            CancellationToken cancellationToken = default)
        {
            ValidateFilters(filters);
            var normalizedDataset = NormalizeDataset(dataset);
            var normalizedFormat = NormalizeFormat(format);
            var requester = await _db.Users
                .AsNoTracking()
                .Where(user => user.UserID == userId)
                .Select(user => new
                {
                    GeneratedBy = (user.FirstName + " " + user.LastName).Trim(),
                    user.FK_StationID,
                    RoleName = user.Role!.Name
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (requester == null || string.IsNullOrWhiteSpace(requester.GeneratedBy))
                throw new DataExchangeValidationException("The exporting user could not be resolved.");

            if (!string.Equals(requester.RoleName, "System Administrator", StringComparison.OrdinalIgnoreCase))
            {
                if (filters.StationId.HasValue && filters.StationId.Value != requester.FK_StationID)
                    throw new DataExchangeValidationException("You may export data only for your assigned station.");
                filters.StationId = requester.FK_StationID;
            }

            var export = normalizedDataset switch
            {
                "dockets" => await BuildDocketsExportAsync(filters, cancellationToken),
                "users" => await BuildUsersExportAsync(filters, cancellationToken),
                "evidence" => await BuildEvidenceExportAsync(filters, cancellationToken),
                "audit" => await BuildAuditExportAsync(filters, cancellationToken),
                _ => throw new DataExchangeValidationException("Unsupported export dataset.")
            };

            var generatedAtUtc = DateTime.UtcNow;
            byte[] content;
            string contentType;
            string extension;
            if (normalizedFormat == "xlsx")
            {
                content = XlsxWorkbookWriter.Build(export, requester.GeneratedBy, generatedAtUtc);
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                extension = "xlsx";
            }
            else if (normalizedFormat == "json")
            {
                content = BuildJsonExport(export, requester.GeneratedBy, generatedAtUtc);
                contentType = "application/json; charset=utf-8";
                extension = "json";
            }
            else if (normalizedFormat == "xml")
            {
                content = BuildXmlExport(export, requester.GeneratedBy, generatedAtUtc);
                contentType = "application/xml; charset=utf-8";
                extension = "xml";
            }
            else
            {
                content = BuildWordExport(export, requester.GeneratedBy, generatedAtUtc);
                contentType = "application/msword";
                extension = "doc";
            }

            var fileName = $"saps-ddms-{normalizedDataset}-{generatedAtUtc:yyyyMMdd-HHmmss}.{extension}";
            return new DataExchangeExportFile(
                content,
                contentType,
                fileName,
                normalizedDataset,
                normalizedFormat,
                export.Rows.Count);
        }

        public async Task<LookupImportPreviewDto> PreviewLookupImportAsync(
            LookupImportDocumentDto document,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var normalized = NormalizeImport(document);
            return await BuildImportPreviewAsync(normalized, userId, issueToken: true, cancellationToken);
        }

        public async Task<LookupImportApplyResultDto> ApplyLookupImportAsync(
            LookupImportDocumentDto document,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (!document.ConfirmApply)
                throw new DataExchangeValidationException("Confirm the reviewed import before applying changes.");
            if (string.IsNullOrWhiteSpace(document.PreviewToken))
                throw new DataExchangeValidationException("Preview this JSON document before applying it.");

            var normalized = NormalizeImport(document);
            var preview = await BuildImportPreviewAsync(
                normalized,
                userId,
                issueToken: false,
                cancellationToken);
            if (!preview.CanApply)
                throw new DataExchangeValidationException(
                    preview.Errors.FirstOrDefault() ?? "The lookup import is not valid.");

            ValidatePreviewToken(
                document.PreviewToken,
                userId,
                normalized.LookupType,
                normalized.ContentHash);

            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var created = 0;
            var updated = 0;
            var unchanged = 0;

            switch (normalized.LookupType)
            {
                case "evidence-types":
                {
                    var existing = ToNameDictionary(
                        await _db.EvidenceTypes.ToListAsync(cancellationToken),
                        item => item.Name);
                    foreach (var record in normalized.Records)
                    {
                        if (existing.ContainsKey(record.Name))
                        {
                            unchanged++;
                            continue;
                        }

                        var entity = new EvidenceType
                        {
                            Name = record.Name,
                            Evidences = new List<Evidence>()
                        };
                        await _db.EvidenceTypes.AddAsync(entity, cancellationToken);
                        existing[record.Name] = entity;
                        created++;
                    }
                    break;
                }
                case "incident-types":
                {
                    var existing = ToNameDictionary(
                        await _db.IncidentTypes.ToListAsync(cancellationToken),
                        item => item.TypeName);
                    foreach (var record in normalized.Records)
                    {
                        if (!existing.TryGetValue(record.Name, out var entity))
                        {
                            entity = new IncidentType
                            {
                                TypeName = record.Name,
                                Description = record.Description ?? string.Empty,
                                Incidents = new List<Incident>()
                            };
                            await _db.IncidentTypes.AddAsync(entity, cancellationToken);
                            existing[record.Name] = entity;
                            created++;
                            continue;
                        }

                        if (record.Description != null &&
                            !string.Equals(entity.Description, record.Description, StringComparison.Ordinal))
                        {
                            entity.Description = record.Description;
                            updated++;
                        }
                        else
                        {
                            unchanged++;
                        }
                    }
                    break;
                }
                case "evidence-locations":
                {
                    var existing = ToNameDictionary(
                        await _db.EvidenceLocations.ToListAsync(cancellationToken),
                        item => item.LocationName);
                    foreach (var record in normalized.Records)
                    {
                        if (!existing.TryGetValue(record.Name, out var entity))
                        {
                            entity = new EvidenceLocation
                            {
                                LocationName = record.Name,
                                PhysicalAddress = record.PhysicalAddress,
                                StorageRequirements = record.StorageRequirements ?? string.Empty,
                                Evidences = new List<Evidence>()
                            };
                            await _db.EvidenceLocations.AddAsync(entity, cancellationToken);
                            existing[record.Name] = entity;
                            created++;
                            continue;
                        }

                        var changed = false;
                        if (record.PhysicalAddress != null &&
                            !string.Equals(entity.PhysicalAddress, record.PhysicalAddress, StringComparison.Ordinal))
                        {
                            entity.PhysicalAddress = record.PhysicalAddress;
                            changed = true;
                        }
                        if (record.StorageRequirements != null &&
                            !string.Equals(entity.StorageRequirements, record.StorageRequirements, StringComparison.Ordinal))
                        {
                            entity.StorageRequirements = record.StorageRequirements;
                            changed = true;
                        }

                        if (changed) updated++; else unchanged++;
                    }
                    break;
                }
                default:
                    throw new DataExchangeValidationException("Unsupported lookup type.");
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogActionAsync(
                "IMPORT_LOOKUPS_JSON",
                "DataExchange",
                null,
                null,
                new
                {
                    normalized.LookupType,
                    Created = created,
                    Updated = updated,
                    Unchanged = unchanged,
                    normalized.ContentHash
                },
                userId,
                "Administrator previewed and confirmed a versioned JSON lookup import.");
            await transaction.CommitAsync(cancellationToken);

            return new LookupImportApplyResultDto
            {
                Success = true,
                Message = $"Lookup import applied: {created} created, {updated} updated, {unchanged} unchanged.",
                LookupType = normalized.LookupType,
                Created = created,
                Updated = updated,
                Unchanged = unchanged,
                ContentHash = normalized.ContentHash
            };
        }

        private async Task<TabularExport> BuildDocketsExportAsync(
            DataExchangeFilterDto filters,
            CancellationToken cancellationToken)
        {
            var query = _db.Dockets.AsNoTracking().AsQueryable();
            ApplyDateRange(ref query, filters, docket => docket.DateCreated);
            if (filters.StationId.HasValue)
                query = query.Where(docket => docket.FK_StationID == filters.StationId.Value);
            if (!string.IsNullOrWhiteSpace(filters.Status))
            {
                var status = filters.Status.Trim();
                query = query.Where(docket => docket.DocketStatus.StatusName == status);
            }

            var records = await query
                .OrderByDescending(docket => docket.DateCreated)
                .Take(MaxExportRows + 1)
                .Select(docket => new DocketExportRow
                {
                    DocketNumber = docket.DocketNumber,
                    ObReference = docket.OBReferenceNumber,
                    FicReference = docket.FICReferenceNumber,
                    Status = docket.DocketStatus.StatusName,
                    Station = docket.Station.StationName,
                    Priority = docket.PriorityLevel,
                    CaseType = docket.CaseType,
                    IncidentDate = docket.IncidentDate,
                    DateCreated = docket.DateCreated,
                    DateAllocated = docket.DateAllocated,
                    CreatedBy = (docket.CreatedByUser.FirstName + " " + docket.CreatedByUser.LastName).Trim()
                })
                .ToListAsync(cancellationToken);
            EnsureExportLimit(records.Count);

            return new TabularExport(
                "dockets",
                "Docket Register",
                new[]
                {
                    "Docket Number", "OB Reference", "FIC Reference", "Status", "Station",
                    "Priority", "Case Type", "Incident Date", "Date Created", "Date Allocated", "Created By"
                },
                records.Select(record => new object?[]
                {
                    record.DocketNumber, record.ObReference, record.FicReference, record.Status, record.Station,
                    record.Priority, record.CaseType, record.IncidentDate, record.DateCreated, record.DateAllocated,
                    record.CreatedBy
                }).ToList(),
                DescribeFilters(filters));
        }

        private async Task<TabularExport> BuildUsersExportAsync(
            DataExchangeFilterDto filters,
            CancellationToken cancellationToken)
        {
            var query = _db.Users.AsNoTracking().AsQueryable();
            ApplyDateRange(ref query, filters, user => user.DateCreated);
            if (filters.StationId.HasValue)
                query = query.Where(user => user.FK_StationID == filters.StationId.Value);
            if (filters.RoleId.HasValue)
                query = query.Where(user => user.FK_RoleID == filters.RoleId.Value);
            if (filters.IsActive.HasValue)
                query = query.Where(user => user.IsActive == filters.IsActive.Value);

            var records = await query
                .OrderBy(user => user.LastName)
                .ThenBy(user => user.FirstName)
                .Take(MaxExportRows + 1)
                .Select(user => new UserExportRow
                {
                    PersonnelNumber = user.PersonnelNumber,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.EmailAddress,
                    Rank = user.Rank,
                    Role = user.Role!.Name,
                    Station = user.Station.StationName,
                    AccountStatus = user.AccountStatus,
                    IsActive = user.IsActive,
                    IsActivated = user.IsActivated,
                    LastLogin = user.LastLoginDate,
                    DateCreated = user.DateCreated
                })
                .ToListAsync(cancellationToken);
            EnsureExportLimit(records.Count);

            return new TabularExport(
                "users",
                "Authorised Personnel Register",
                new[]
                {
                    "Personnel Number", "First Name", "Last Name", "Email", "Rank", "Role", "Station",
                    "Account Status", "Active", "Activated", "Last Login", "Date Created"
                },
                records.Select(record => new object?[]
                {
                    record.PersonnelNumber, record.FirstName, record.LastName, record.Email, record.Rank,
                    record.Role, record.Station, record.AccountStatus, record.IsActive, record.IsActivated,
                    record.LastLogin, record.DateCreated
                }).ToList(),
                DescribeFilters(filters));
        }

        private async Task<TabularExport> BuildEvidenceExportAsync(
            DataExchangeFilterDto filters,
            CancellationToken cancellationToken)
        {
            var query = _db.Evidences.AsNoTracking().AsQueryable();
            ApplyDateRange(ref query, filters, evidence => evidence.DateCollected);
            if (filters.StationId.HasValue)
                query = query.Where(evidence => evidence.Docket!.FK_StationID == filters.StationId.Value);
            if (filters.DocketId.HasValue)
                query = query.Where(evidence => evidence.FK_DocketID == filters.DocketId.Value);

            var records = await query
                .OrderByDescending(evidence => evidence.DateCollected)
                .Take(MaxExportRows + 1)
                .Select(evidence => new EvidenceExportRow
                {
                    Reference = evidence.EvidenceReferenceNumber,
                    DocketNumber = evidence.Docket!.DocketNumber,
                    Description = evidence.Description,
                    Type = evidence.EvidenceType!.Name,
                    Location = evidence.EvidenceLocation!.LocationName,
                    Quantity = evidence.Quantity,
                    DateCollected = evidence.DateCollected,
                    CollectionMethod = evidence.CollectionMethod,
                    StateOfItem = evidence.StateOfItem,
                    SignatureMethod = evidence.SignatureMethod,
                    CollectedBy = (evidence.User!.FirstName + " " + evidence.User.LastName).Trim()
                })
                .ToListAsync(cancellationToken);
            EnsureExportLimit(records.Count);

            return new TabularExport(
                "evidence",
                "Evidence Register",
                new[]
                {
                    "Evidence Reference", "Docket Number", "Description", "Type", "Storage Location",
                    "Quantity", "Date Collected", "Collection Method", "State of Item", "Signature Method",
                    "Collected By"
                },
                records.Select(record => new object?[]
                {
                    record.Reference, record.DocketNumber, record.Description, record.Type, record.Location,
                    record.Quantity, record.DateCollected, record.CollectionMethod, record.StateOfItem,
                    record.SignatureMethod, record.CollectedBy
                }).ToList(),
                DescribeFilters(filters));
        }

        private async Task<TabularExport> BuildAuditExportAsync(
            DataExchangeFilterDto filters,
            CancellationToken cancellationToken)
        {
            var query = _db.UserAuditLogs.AsNoTracking().AsQueryable();
            ApplyDateRange(ref query, filters, log => log.Timestamp);
            if (filters.UserId.HasValue)
                query = query.Where(log => log.FK_UserID == filters.UserId.Value);
            if (!string.IsNullOrWhiteSpace(filters.ActionType))
            {
                var action = filters.ActionType.Trim();
                query = query.Where(log => log.ActionType == action);
            }
            if (filters.StationId.HasValue)
                query = query.Where(log => log.User.FK_StationID == filters.StationId.Value);

            var records = await query
                .OrderByDescending(log => log.Timestamp)
                .Take(MaxExportRows + 1)
                .Select(log => new AuditExportRow
                {
                    Timestamp = log.Timestamp,
                    PersonnelNumber = log.User.PersonnelNumber,
                    UserName = (log.User.FirstName + " " + log.User.LastName).Trim(),
                    ActionType = log.ActionType,
                    Entity = log.AffectedEntity,
                    EntityId = log.AffectedEntityID,
                    Reason = log.Reason,
                    IpAddress = log.IPAddress
                })
                .ToListAsync(cancellationToken);
            EnsureExportLimit(records.Count);

            return new TabularExport(
                "audit",
                "Audit Trail Export",
                new[]
                {
                    "Timestamp", "Personnel Number", "User", "Action", "Affected Entity",
                    "Affected Entity ID", "Reason", "IP Address"
                },
                records.Select(record => new object?[]
                {
                    record.Timestamp, record.PersonnelNumber, record.UserName, record.ActionType,
                    record.Entity, record.EntityId, record.Reason, record.IpAddress
                }).ToList(),
                DescribeFilters(filters));
        }

        private async Task<LookupImportPreviewDto> BuildImportPreviewAsync(
            NormalizedLookupImport normalized,
            int userId,
            bool issueToken,
            CancellationToken cancellationToken)
        {
            var rows = new List<LookupImportPreviewRowDto>();
            var errors = normalized.Errors.ToList();

            if (errors.Count == 0)
            {
                switch (normalized.LookupType)
                {
                    case "evidence-types":
                    {
                        var existing = ToNameDictionary(
                            await _db.EvidenceTypes.AsNoTracking().ToListAsync(cancellationToken),
                            item => item.Name);
                        rows.AddRange(normalized.Records.Select(record => new LookupImportPreviewRowDto
                        {
                            Name = record.Name,
                            Action = existing.ContainsKey(record.Name) ? "Unchanged" : "Create",
                            Details = existing.ContainsKey(record.Name)
                                ? "An evidence type with this name already exists."
                                : "A new evidence type will be added."
                        }));
                        break;
                    }
                    case "incident-types":
                    {
                        var existing = ToNameDictionary(
                            await _db.IncidentTypes.AsNoTracking().ToListAsync(cancellationToken),
                            item => item.TypeName);
                        foreach (var record in normalized.Records)
                        {
                            if (!existing.TryGetValue(record.Name, out var entity))
                            {
                                rows.Add(PreviewRow(record.Name, "Create", "A new incident type will be added."));
                            }
                            else if (record.Description != null &&
                                     !string.Equals(entity.Description, record.Description, StringComparison.Ordinal))
                            {
                                rows.Add(PreviewRow(record.Name, "Update", "Description will be updated."));
                            }
                            else
                            {
                                rows.Add(PreviewRow(record.Name, "Unchanged", "No values will change."));
                            }
                        }
                        break;
                    }
                    case "evidence-locations":
                    {
                        var existing = ToNameDictionary(
                            await _db.EvidenceLocations.AsNoTracking().ToListAsync(cancellationToken),
                            item => item.LocationName);
                        foreach (var record in normalized.Records)
                        {
                            if (!existing.TryGetValue(record.Name, out var entity))
                            {
                                rows.Add(PreviewRow(record.Name, "Create", "A new evidence location will be added."));
                                continue;
                            }

                            var changes = new List<string>();
                            if (record.PhysicalAddress != null &&
                                !string.Equals(entity.PhysicalAddress, record.PhysicalAddress, StringComparison.Ordinal))
                                changes.Add("physical address");
                            if (record.StorageRequirements != null &&
                                !string.Equals(entity.StorageRequirements, record.StorageRequirements, StringComparison.Ordinal))
                                changes.Add("storage requirements");

                            rows.Add(changes.Count == 0
                                ? PreviewRow(record.Name, "Unchanged", "No values will change.")
                                : PreviewRow(record.Name, "Update", $"Will update {string.Join(" and ", changes)}."));
                        }
                        break;
                    }
                }
            }

            var createCount = rows.Count(row => row.Action == "Create");
            var updateCount = rows.Count(row => row.Action == "Update");
            var unchangedCount = rows.Count(row => row.Action == "Unchanged");
            var canApply = errors.Count == 0 && normalized.Records.Count > 0;
            string? token = null;
            DateTime? expiresAtUtc = null;
            if (canApply && issueToken)
            {
                expiresAtUtc = DateTime.UtcNow.Add(PreviewLifetime);
                token = CreatePreviewToken(
                    userId,
                    normalized.LookupType,
                    normalized.ContentHash,
                    expiresAtUtc.Value);
            }

            return new LookupImportPreviewDto
            {
                LookupType = normalized.LookupType,
                ContentHash = normalized.ContentHash,
                TotalRecords = normalized.Records.Count,
                CreateCount = createCount,
                UpdateCount = updateCount,
                UnchangedCount = unchangedCount,
                CanApply = canApply,
                PreviewToken = token,
                PreviewExpiresAtUtc = expiresAtUtc,
                Errors = errors,
                Rows = rows
            };
        }

        private static LookupImportPreviewRowDto PreviewRow(string name, string action, string details)
        {
            return new LookupImportPreviewRowDto { Name = name, Action = action, Details = details };
        }

        private NormalizedLookupImport NormalizeImport(LookupImportDocumentDto document)
        {
            var errors = new List<string>();
            if (document.SchemaVersion != 1)
                errors.Add("Only schemaVersion 1 is supported.");

            var lookupType = (document.LookupType ?? string.Empty).Trim().ToLowerInvariant();
            if (lookupType is not ("evidence-types" or "incident-types" or "evidence-locations"))
                errors.Add("lookupType must be evidence-types, incident-types, or evidence-locations.");
            if (document.AdditionalFields?.Count > 0)
                errors.Add($"Unsupported document properties: {string.Join(", ", document.AdditionalFields.Keys)}.");

            var sourceRecords = document.Records ?? new List<LookupImportRecordDto>();
            if (sourceRecords.Count == 0)
                errors.Add("At least one lookup record is required.");
            if (sourceRecords.Count > MaxImportRows)
                errors.Add($"A lookup import may contain no more than {MaxImportRows} records.");

            var normalizedRecords = new List<NormalizedLookupRecord>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Math.Min(sourceRecords.Count, MaxImportRows); index++)
            {
                var record = sourceRecords[index];
                var rowNumber = index + 1;
                var name = (record.Name ?? string.Empty).Trim();
                var description = NormalizeOptional(record.Description);
                var physicalAddress = NormalizeOptional(record.PhysicalAddress);
                var storageRequirements = NormalizeOptional(record.StorageRequirements);

                if (record.AdditionalFields?.Count > 0)
                    errors.Add($"Record {rowNumber} contains unsupported properties: {string.Join(", ", record.AdditionalFields.Keys)}.");
                if (string.IsNullOrWhiteSpace(name))
                    errors.Add($"Record {rowNumber}: name is required.");
                if (!names.Add(name))
                    errors.Add($"Record {rowNumber}: duplicate name '{name}'.");

                switch (lookupType)
                {
                    case "evidence-types":
                        if (name.Length > 20)
                            errors.Add($"Record {rowNumber}: evidence type name may not exceed 20 characters.");
                        if (description != null || physicalAddress != null || storageRequirements != null)
                            errors.Add($"Record {rowNumber}: evidence-types accepts only the name property.");
                        break;
                    case "incident-types":
                        if (name.Length > 256)
                            errors.Add($"Record {rowNumber}: incident type name may not exceed 256 characters.");
                        if (description?.Length > 256)
                            errors.Add($"Record {rowNumber}: description may not exceed 256 characters.");
                        if (physicalAddress != null || storageRequirements != null)
                            errors.Add($"Record {rowNumber}: incident-types does not accept location properties.");
                        break;
                    case "evidence-locations":
                        if (name.Length > 60)
                            errors.Add($"Record {rowNumber}: evidence location name may not exceed 60 characters.");
                        if (physicalAddress?.Length > 100)
                            errors.Add($"Record {rowNumber}: physicalAddress may not exceed 100 characters.");
                        if (storageRequirements?.Length > 256)
                            errors.Add($"Record {rowNumber}: storageRequirements may not exceed 256 characters.");
                        if (description != null)
                            errors.Add($"Record {rowNumber}: evidence-locations does not accept description.");
                        break;
                }

                normalizedRecords.Add(new NormalizedLookupRecord(
                    name,
                    description,
                    physicalAddress,
                    storageRequirements));
            }

            var canonical = JsonSerializer.Serialize(new
            {
                SchemaVersion = 1,
                LookupType = lookupType,
                Records = normalizedRecords.Select(record => new
                {
                    record.Name,
                    record.Description,
                    record.PhysicalAddress,
                    record.StorageRequirements
                })
            });
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            return new NormalizedLookupImport(lookupType, normalizedRecords, errors, hash);
        }

        private string CreatePreviewToken(
            int userId,
            string lookupType,
            string contentHash,
            DateTime expiresAtUtc)
        {
            var payload = JsonSerializer.Serialize(new PreviewTokenPayload
            {
                UserId = userId,
                LookupType = lookupType,
                ContentHash = contentHash,
                ExpiresAtUtc = expiresAtUtc
            });
            return _previewProtector.Protect(payload);
        }

        private void ValidatePreviewToken(
            string token,
            int userId,
            string lookupType,
            string contentHash)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<PreviewTokenPayload>(
                    _previewProtector.Unprotect(token));
                if (payload == null ||
                    payload.UserId != userId ||
                    !string.Equals(payload.LookupType, lookupType, StringComparison.Ordinal) ||
                    !CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(payload.ContentHash),
                        Encoding.UTF8.GetBytes(contentHash)) ||
                    payload.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    throw new DataExchangeValidationException(
                        "The import preview is invalid or expired. Preview the JSON file again.");
                }
            }
            catch (DataExchangeValidationException)
            {
                throw;
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException)
            {
                throw new DataExchangeValidationException(
                    "The import preview is invalid or expired. Preview the JSON file again.");
            }
        }

        private static string NormalizeDataset(string dataset)
        {
            var normalized = (dataset ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "docket" or "dockets" => "dockets",
                "user" or "users" => "users",
                "evidence" => "evidence",
                "audit" or "audit-log" or "audit-logs" => "audit",
                _ => throw new DataExchangeValidationException(
                    "dataset must be dockets, users, evidence, or audit.")
            };
        }

        private static string NormalizeFormat(string format)
        {
            var normalized = (format ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "xlsx" or "excel" => "xlsx",
                "json" => "json",
                "xml" => "xml",
                "doc" or "word" => "doc",
                _ => throw new DataExchangeValidationException("format must be xlsx, json, xml, or doc.")
            };
        }

        private static void ValidateFilters(DataExchangeFilterDto filters)
        {
            if (filters.From.HasValue && filters.To.HasValue && filters.From.Value.Date > filters.To.Value.Date)
                throw new DataExchangeValidationException("The From date must be on or before the To date.");

            var identifiers = new[]
            {
                filters.StationId, filters.RoleId, filters.DocketId, filters.UserId
            };
            if (identifiers.Any(identifier => identifier.HasValue && identifier.Value <= 0))
                throw new DataExchangeValidationException("Filter identifiers must be positive numbers.");
        }

        private static void ApplyDateRange<TEntity>(
            ref IQueryable<TEntity> query,
            DataExchangeFilterDto filters,
            System.Linq.Expressions.Expression<Func<TEntity, DateTime>> selector)
        {
            if (filters.From.HasValue)
            {
                var from = filters.From.Value.Date;
                var parameter = selector.Parameters[0];
                var comparison = System.Linq.Expressions.Expression.GreaterThanOrEqual(
                    selector.Body,
                    System.Linq.Expressions.Expression.Constant(from));
                query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<TEntity, bool>>(comparison, parameter));
            }
            if (filters.To.HasValue)
            {
                var toExclusive = filters.To.Value.Date.AddDays(1);
                var parameter = selector.Parameters[0];
                var comparison = System.Linq.Expressions.Expression.LessThan(
                    selector.Body,
                    System.Linq.Expressions.Expression.Constant(toExclusive));
                query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<TEntity, bool>>(comparison, parameter));
            }
        }

        private static void EnsureExportLimit(int count)
        {
            if (count > MaxExportRows)
                throw new DataExchangeValidationException(
                    $"The export exceeds {MaxExportRows:N0} rows. Add date, station, or dataset-specific filters.");
        }

        private static IReadOnlyList<KeyValuePair<string, string>> DescribeFilters(DataExchangeFilterDto filters)
        {
            var values = new List<KeyValuePair<string, string>>();
            AddFilter(values, "From", filters.From?.ToString("yyyy-MM-dd"));
            AddFilter(values, "To", filters.To?.ToString("yyyy-MM-dd"));
            AddFilter(values, "Station ID", filters.StationId?.ToString(CultureInfo.InvariantCulture));
            AddFilter(values, "Status", filters.Status?.Trim());
            AddFilter(values, "Role ID", filters.RoleId?.ToString(CultureInfo.InvariantCulture));
            AddFilter(values, "Active", filters.IsActive?.ToString());
            AddFilter(values, "Docket ID", filters.DocketId?.ToString(CultureInfo.InvariantCulture));
            AddFilter(values, "User ID", filters.UserId?.ToString(CultureInfo.InvariantCulture));
            AddFilter(values, "Action Type", filters.ActionType?.Trim());
            if (values.Count == 0)
                values.Add(new KeyValuePair<string, string>("Filters", "All authorised records"));
            return values;
        }

        private static void AddFilter(
            ICollection<KeyValuePair<string, string>> filters,
            string label,
            string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                filters.Add(new KeyValuePair<string, string>(label, value));
        }

        private static byte[] BuildJsonExport(
            TabularExport export,
            string generatedBy,
            DateTime generatedAtUtc)
        {
            var rows = export.Rows.Select(row =>
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var index = 0; index < export.Columns.Count; index++)
                    result[export.Columns[index]] = index < row.Length ? row[index] : null;
                return result;
            }).ToList();

            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                product = "SAPS Digital Docket Management System",
                dataset = export.Key,
                title = export.Title,
                generatedAtUtc,
                generatedBy,
                filters = export.Filters.ToDictionary(item => item.Key, item => item.Value),
                columns = export.Columns,
                rowCount = rows.Count,
                rows
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        private static byte[] BuildXmlExport(
            TabularExport export,
            string generatedBy,
            DateTime generatedAtUtc)
        {
            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                CloseOutput = false
            }))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("SapsDdmsExport");
                writer.WriteAttributeString("schemaVersion", "1");
                writer.WriteElementString("Dataset", export.Key);
                writer.WriteElementString("Title", export.Title);
                writer.WriteElementString("GeneratedAtUtc", generatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteElementString("GeneratedBy", generatedBy);
                writer.WriteStartElement("Filters");
                foreach (var filter in export.Filters)
                {
                    writer.WriteStartElement("Filter");
                    writer.WriteAttributeString("name", filter.Key);
                    writer.WriteString(filter.Value);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("Rows");
                writer.WriteAttributeString("count", export.Rows.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var row in export.Rows)
                {
                    writer.WriteStartElement("Row");
                    for (var index = 0; index < export.Columns.Count; index++)
                    {
                        writer.WriteStartElement("Field");
                        writer.WriteAttributeString("name", export.Columns[index]);
                        if (index < row.Length && row[index] != null)
                            writer.WriteString(FormatExportValue(row[index]));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            return stream.ToArray();
        }

        private static byte[] BuildWordExport(
            TabularExport export,
            string generatedBy,
            DateTime generatedAtUtc)
        {
            static string Encode(object? value) => SecurityElement.Escape(FormatExportValue(value)) ?? string.Empty;
            var document = new StringBuilder("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>")
                .Append(Encode(export.Title))
                .Append("</title><style>body{font-family:Arial,sans-serif;color:#102f4f}table{border-collapse:collapse;width:100%;font-size:9pt}th,td{border:1px solid #9aa9b8;padding:5px;text-align:left}th{background:#0b3a67;color:white}h1{color:#0b3a67}.meta{margin-bottom:16px}</style></head><body>")
                .Append("<h1>").Append(Encode(export.Title)).Append("</h1>")
                .Append("<div class=\"meta\"><strong>SAPS Digital Docket Management System</strong><br>Generated by: ")
                .Append(Encode(generatedBy)).Append("<br>Generated: ")
                .Append(generatedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))
                .Append("<br>Records: ").Append(export.Rows.Count).Append("</div><table><thead><tr>");
            foreach (var column in export.Columns) document.Append("<th>").Append(Encode(column)).Append("</th>");
            document.Append("</tr></thead><tbody>");
            foreach (var row in export.Rows)
            {
                document.Append("<tr>");
                for (var index = 0; index < export.Columns.Count; index++)
                    document.Append("<td>").Append(Encode(index < row.Length ? row[index] : null)).Append("</td>");
                document.Append("</tr>");
            }
            document.Append("</tbody></table></body></html>");
            return new UTF8Encoding(true).GetBytes(document.ToString());
        }

        private static string FormatExportValue(object? value) => value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };

        private static Dictionary<string, T> ToNameDictionary<T>(
            IEnumerable<T> values,
            Func<T, string?> nameSelector)
        {
            return values
                .Where(value => !string.IsNullOrWhiteSpace(nameSelector(value)))
                .GroupBy(value => nameSelector(value)!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        private static string? NormalizeOptional(string? value)
        {
            return value == null ? null : value.Trim();
        }

        private sealed record TabularExport(
            string Key,
            string Title,
            IReadOnlyList<string> Columns,
            List<object?[]> Rows,
            IReadOnlyList<KeyValuePair<string, string>> Filters);

        private sealed record NormalizedLookupRecord(
            string Name,
            string? Description,
            string? PhysicalAddress,
            string? StorageRequirements);

        private sealed record NormalizedLookupImport(
            string LookupType,
            List<NormalizedLookupRecord> Records,
            List<string> Errors,
            string ContentHash);

        private sealed class PreviewTokenPayload
        {
            public int UserId { get; set; }
            public string LookupType { get; set; } = string.Empty;
            public string ContentHash { get; set; } = string.Empty;
            public DateTime ExpiresAtUtc { get; set; }
        }

        private sealed class DocketExportRow
        {
            public string? DocketNumber { get; init; }
            public string? ObReference { get; init; }
            public string? FicReference { get; init; }
            public string? Status { get; init; }
            public string? Station { get; init; }
            public string? Priority { get; init; }
            public string? CaseType { get; init; }
            public DateTime IncidentDate { get; init; }
            public DateTime DateCreated { get; init; }
            public DateTime? DateAllocated { get; init; }
            public string? CreatedBy { get; init; }
        }

        private sealed class UserExportRow
        {
            public string? PersonnelNumber { get; init; }
            public string? FirstName { get; init; }
            public string? LastName { get; init; }
            public string? Email { get; init; }
            public string? Rank { get; init; }
            public string? Role { get; init; }
            public string? Station { get; init; }
            public string? AccountStatus { get; init; }
            public bool IsActive { get; init; }
            public bool IsActivated { get; init; }
            public DateTime? LastLogin { get; init; }
            public DateTime DateCreated { get; init; }
        }

        private sealed class EvidenceExportRow
        {
            public string? Reference { get; init; }
            public string? DocketNumber { get; init; }
            public string? Description { get; init; }
            public string? Type { get; init; }
            public string? Location { get; init; }
            public int Quantity { get; init; }
            public DateTime DateCollected { get; init; }
            public string? CollectionMethod { get; init; }
            public string? StateOfItem { get; init; }
            public string? SignatureMethod { get; init; }
            public string? CollectedBy { get; init; }
        }

        private sealed class AuditExportRow
        {
            public DateTime Timestamp { get; init; }
            public string? PersonnelNumber { get; init; }
            public string? UserName { get; init; }
            public string? ActionType { get; init; }
            public string? Entity { get; init; }
            public int? EntityId { get; init; }
            public string? Reason { get; init; }
            public string? IpAddress { get; init; }
        }

        private static class XlsxWorkbookWriter
        {
            private static readonly UTF8Encoding Utf8WithoutBom = new(false);

            public static byte[] Build(
                TabularExport export,
                string generatedBy,
                DateTime generatedAtUtc)
            {
                using var stream = new MemoryStream();
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    WriteEntry(archive, "[Content_Types].xml", ContentTypesXml());
                    WriteEntry(archive, "_rels/.rels", RootRelationshipsXml());
                    WriteEntry(archive, "docProps/core.xml", CorePropertiesXml(export.Title, generatedBy, generatedAtUtc));
                    WriteEntry(archive, "docProps/app.xml", AppPropertiesXml());
                    WriteEntry(archive, "xl/workbook.xml", WorkbookXml());
                    WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml());
                    WriteEntry(archive, "xl/styles.xml", StylesXml());
                    WriteEntry(
                        archive,
                        "xl/worksheets/sheet1.xml",
                        WorksheetXml(export.Columns, export.Rows, labelFirstColumn: false));

                    var metadataRows = new List<object?[]>
                    {
                        new object?[] { "Product", "SAPS Digital Docket Management System" },
                        new object?[] { "Report", export.Title },
                        new object?[] { "Dataset", export.Key },
                        new object?[] { "Generated At (UTC)", generatedAtUtc },
                        new object?[] { "Generated By", generatedBy },
                        new object?[] { "Row Count", export.Rows.Count }
                    };
                    metadataRows.AddRange(export.Filters.Select(filter =>
                        new object?[] { $"Filter - {filter.Key}", filter.Value }));
                    WriteEntry(
                        archive,
                        "xl/worksheets/sheet2.xml",
                        WorksheetXml(
                            new[] { "Property", "Value" },
                            metadataRows,
                            labelFirstColumn: true));
                }
                return stream.ToArray();
            }

            private static void WriteEntry(ZipArchive archive, string path, string content)
            {
                var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), Utf8WithoutBom);
                writer.Write(content);
            }

            private static string WorksheetXml(
                IReadOnlyList<string> columns,
                IReadOnlyList<object?[]> rows,
                bool labelFirstColumn)
            {
                var allRows = new List<object?[]> { columns.Cast<object?>().ToArray() };
                allRows.AddRange(rows);
                var lastColumn = ColumnName(Math.Max(1, columns.Count));
                var widths = Enumerable.Range(0, columns.Count)
                    .Select(index => CalculateWidth(index, allRows))
                    .ToArray();

                var builder = new StringBuilder();
                builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
                    .Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">")
                    .Append($"<dimension ref=\"A1:{lastColumn}{Math.Max(1, allRows.Count)}\"/>")
                    .Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>")
                    .Append("<sheetFormatPr defaultRowHeight=\"15\"/>")
                    .Append("<cols>");
                for (var index = 0; index < widths.Length; index++)
                    builder.Append($"<col min=\"{index + 1}\" max=\"{index + 1}\" width=\"{widths[index].ToString("0.0", CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                builder.Append("</cols><sheetData>");

                for (var rowIndex = 0; rowIndex < allRows.Count; rowIndex++)
                {
                    var excelRow = rowIndex + 1;
                    builder.Append($"<row r=\"{excelRow}\">");
                    var row = allRows[rowIndex];
                    for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    {
                        var value = columnIndex < row.Length ? row[columnIndex] : null;
                        var style = rowIndex == 0 ? 1 : labelFirstColumn && columnIndex == 0 ? 2 : 3;
                        builder.Append(CellXml(
                            $"{ColumnName(columnIndex + 1)}{excelRow}",
                            value,
                            style));
                    }
                    builder.Append("</row>");
                }

                builder.Append("</sheetData>")
                    .Append($"<autoFilter ref=\"A1:{lastColumn}1\"/>")
                    .Append("</worksheet>");
                return builder.ToString();
            }

            private static string CellXml(string reference, object? value, int style)
            {
                if (value == null)
                    return $"<c r=\"{reference}\" s=\"{style}\"/>";
                if (value is bool boolean)
                    return $"<c r=\"{reference}\" s=\"{style}\" t=\"b\"><v>{(boolean ? 1 : 0)}</v></c>";
                if (value is DateTime date)
                    return NumericDateCell(reference, date.ToUniversalTime().ToOADate());
                if (value is DateTimeOffset dateOffset)
                    return NumericDateCell(reference, dateOffset.UtcDateTime.ToOADate());
                if (IsNumber(value))
                    return $"<c r=\"{reference}\" s=\"{style}\" t=\"n\"><v>{Convert.ToString(value, CultureInfo.InvariantCulture)}</v></c>";

                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                if (text.Length > 32_767)
                    text = text[..32_760] + "...";
                return $"<c r=\"{reference}\" s=\"{style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{EscapeXml(text)}</t></is></c>";
            }

            private static string NumericDateCell(string reference, double serialValue) =>
                $"<c r=\"{reference}\" s=\"4\" t=\"n\"><v>{serialValue.ToString("0.###############", CultureInfo.InvariantCulture)}</v></c>";

            private static bool IsNumber(object value)
            {
                return value is byte or sbyte or short or ushort or int or uint or long or ulong or
                    float or double or decimal;
            }

            private static double CalculateWidth(int columnIndex, IEnumerable<object?[]> rows)
            {
                var maxLength = rows
                    .Select(row => columnIndex < row.Length
                        ? Convert.ToString(row[columnIndex], CultureInfo.InvariantCulture)?.Length ?? 0
                        : 0)
                    .DefaultIfEmpty(8)
                    .Max();
                return Math.Clamp(maxLength + 2, 10, 45);
            }

            private static string ColumnName(int columnNumber)
            {
                var builder = new StringBuilder();
                while (columnNumber > 0)
                {
                    columnNumber--;
                    builder.Insert(0, (char)('A' + (columnNumber % 26)));
                    columnNumber /= 26;
                }
                return builder.ToString();
            }

            private static string EscapeXml(string value)
            {
                var valid = string.Concat(value.Where(XmlConvert.IsXmlChar));
                return SecurityElement.Escape(valid) ?? string.Empty;
            }

            private static string ContentTypesXml() =>
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
                "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>" +
                "</Types>";

            private static string RootRelationshipsXml() =>
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
                "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>" +
                "</Relationships>";

            private static string WorkbookXml() =>
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<sheets><sheet name=\"Data\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Metadata\" sheetId=\"2\" r:id=\"rId2\"/></sheets>" +
                "</workbook>";

            private static string WorkbookRelationshipsXml() =>
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
                "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "</Relationships>";

            private static string StylesXml() =>
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"yyyy-mm-dd hh:mm\"/></numFmts>" +
                "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Aptos\"/></font><font><b/><color rgb=\"FFFFFFFF\"/><sz val=\"11\"/><name val=\"Aptos\"/></font></fonts>" +
                "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF003B70\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
                "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style=\"thin\"><color rgb=\"FFD9E2EC\"/></left><right style=\"thin\"><color rgb=\"FFD9E2EC\"/></right><top style=\"thin\"><color rgb=\"FFD9E2EC\"/></top><bottom style=\"thin\"><color rgb=\"FFD9E2EC\"/></bottom><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"5\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\"><alignment horizontal=\"left\"/></xf><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/><xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/></cellXfs>" +
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                "</styleSheet>";

            private static string CorePropertiesXml(
                string title,
                string generatedBy,
                DateTime generatedAtUtc) =>
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                $"<dc:title>{EscapeXml(title)}</dc:title><dc:creator>{EscapeXml(generatedBy)}</dc:creator><cp:lastModifiedBy>{EscapeXml(generatedBy)}</cp:lastModifiedBy>" +
                $"<dcterms:created xsi:type=\"dcterms:W3CDTF\">{generatedAtUtc:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created>" +
                "</cp:coreProperties>";

            private static string AppPropertiesXml() =>
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">" +
                "<Application>SAPS DDMS</Application><AppVersion>1.0</AppVersion>" +
                "</Properties>";
        }
    }
}
