// Services/IEvidenceService.cs
using SAPS.DDMS.API.Helpers;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Repositories;
using System.Security.Cryptography;
using System.Text;

using static SAPS.DDMS.API.Models.DTOs.EvidenceDtos;

namespace SAPS.DDMS.API.Services
{
    public interface IEvidenceService
    {
        Task<ServiceResult<EvidenceResponseDto>> CreateEvidenceAsync(CreateEvidenceDto createDto, int registeredByUserId);
        Task<ServiceResult<EvidenceResponseDto>> GetEvidenceByIdAsync(int evidenceId, int requestingUserId);
        Task<ServiceResult<List<EvidenceResponseDto>>> GetEvidenceByDocketAsync(int docketId, int requestingUserId);
        Task<ServiceResult<bool>> UpdateEvidenceAsync(int evidenceId, UpdateEvidenceDto updateDto, int requestingUserId);
        Task<ServiceResult<List<EvidenceType>>> GetEvidenceTypesAsync();
        Task<ServiceResult<List<EvidenceLocation>>> GetEvidenceLocationsAsync();
    }

    public class EvidenceService : IEvidenceService
    {
        private readonly IEvidenceRepository _evidenceRepository;
        private readonly IDocketRepository _docketRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAuditService _auditService;
        private readonly IFileStorageService _fileStorageService;

        public EvidenceService(
            IEvidenceRepository evidenceRepository,
            IDocketRepository docketRepository,
            IUserRepository userRepository,
            IAuditService auditService,
            IFileStorageService fileStorageService)
        {
            _evidenceRepository = evidenceRepository;
            _docketRepository = docketRepository;
            _userRepository = userRepository;
            _auditService = auditService;
            _fileStorageService = fileStorageService;
        }

        // UC 5.1 - Create Evidence Item
        public async Task<ServiceResult<EvidenceResponseDto>> CreateEvidenceAsync(CreateEvidenceDto createDto, int registeredByUserId)
        {
            if (!TryValidate(createDto, out var validationError))
                return ServiceResult<EvidenceResponseDto>.Failure(validationError);

            var registeredBy = await _userRepository.GetByIdAsync(registeredByUserId);
            if (registeredBy == null)
                return ServiceResult<EvidenceResponseDto>.Failure("User not found.");

            var docket = await _docketRepository.GetByIdAsync(createDto.DocketID);
            if (docket == null)
                return ServiceResult<EvidenceResponseDto>.Failure("Docket not found.");
            if (docket.DocketStatus?.StatusName is "CLOSED" or "ARCHIVED")
                return ServiceResult<EvidenceResponseDto>.Failure("Evidence cannot be added to a closed or archived docket.");

            var isAssigned = await _evidenceRepository.IsUserAssignedToDocketAsync(registeredByUserId, createDto.DocketID);
            var isSystemAdmin = registeredBy.Role?.Name == "System Administrator";
            if (!isAssigned && !isSystemAdmin)
                return ServiceResult<EvidenceResponseDto>.Failure("Only the assigned Investigating Officer may register evidence.");

            if (createDto.DateCollected == default)
                return ServiceResult<EvidenceResponseDto>.Failure("Date collected is required.");

            if (createDto.DateCollected > DateTime.UtcNow)
                return ServiceResult<EvidenceResponseDto>.Failure("Date collected cannot be in the future.");

            if (createDto.Quantity <= 0)
                return ServiceResult<EvidenceResponseDto>.Failure("Quantity must be greater than zero.");

            var evidenceTypes = await _evidenceRepository.GetEvidenceTypesAsync();
            if (!evidenceTypes.Any(type => type.EvidenceTypeID == createDto.EvidenceTypeID))
                return ServiceResult<EvidenceResponseDto>.Failure("The selected evidence type does not exist.");

            var evidenceLocations = await _evidenceRepository.GetEvidenceLocationsAsync();
            if (!evidenceLocations.Any(location => location.EvidenceLocationID == createDto.EvidenceLocationID))
                return ServiceResult<EvidenceResponseDto>.Failure("The selected evidence location does not exist.");

            if (!string.IsNullOrWhiteSpace(createDto.CollectionMethod) &&
                !OperationalLookupCatalog.ContainsIgnoreCase(
                    OperationalLookupCatalog.EvidenceCollectionMethods,
                    createDto.CollectionMethod))
                return ServiceResult<EvidenceResponseDto>.Failure("Select a valid evidence collection method from the provided list.");

            if (!string.IsNullOrWhiteSpace(createDto.StateOfItem) &&
                !OperationalLookupCatalog.ContainsIgnoreCase(
                    OperationalLookupCatalog.EvidenceItemStates,
                    createDto.StateOfItem))
                return ServiceResult<EvidenceResponseDto>.Failure("Select a valid state of item from the provided list.");

            if (!IsValidSignatureMethod(createDto.SignatureMethod))
                return ServiceResult<EvidenceResponseDto>.Failure("Select Ink, Stylus or Smartcard as the signature method.");

            var existingCount = await _evidenceRepository.GetCountByDocketIdAsync(createDto.DocketID);
            var evidenceReferenceNumber = DocketNumberHelper.GenerateEvidenceReferenceNumber(docket.DocketNumber, existingCount + 1);

            string evidenceFilePath = null;
            string evidenceFileHash = null;

            // Evidence attachments may be images, PDF documents or video recordings.
            if (createDto.EvidenceFile != null && createDto.EvidenceFile.Length > 0)
            {
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/jpg", "image/gif", "application/pdf", "video/mp4", "video/webm", "video/quicktime", "video/x-msvideo" };
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".mp4", ".webm", ".mov", ".avi" };
                var extension = Path.GetExtension(createDto.EvidenceFile.FileName).ToLowerInvariant();
                if (!allowedTypes.Contains(createDto.EvidenceFile.ContentType.ToLowerInvariant()) ||
                    !allowedExtensions.Contains(extension))
                    return ServiceResult<EvidenceResponseDto>.Failure("Only JPEG, PNG, GIF, PDF, MP4, WebM, MOV or AVI evidence files are allowed.");

                const long maxEvidenceFileBytes = 50 * 1024 * 1024;
                if (createDto.EvidenceFile.Length > maxEvidenceFileBytes)
                    return ServiceResult<EvidenceResponseDto>.Failure("Evidence files cannot exceed 50 MB.");

                evidenceFileHash = _fileStorageService.ComputeFileHash(createDto.EvidenceFile);
                evidenceFilePath = await _fileStorageService.SaveFileAsync(createDto.EvidenceFile, "evidence");
            }

            var payloadHash = ComputeEvidencePayloadHash(createDto, evidenceFileHash);

            var evidence = new Evidence
            {
                EvidenceReferenceNumber = evidenceReferenceNumber,
                Description = createDto.Description!.Trim(),
                Quantity = createDto.Quantity,
                DateCollected = createDto.DateCollected,
                CollectionMethod = createDto.CollectionMethod!.Trim(),
                StateOfItem = createDto.StateOfItem!.Trim(),
                ImagePath = evidenceFilePath,
                FileHash = evidenceFileHash,
                SignatureMethod = createDto.SignatureMethod!.Trim(),
                PayloadHash = payloadHash,
                FK_EvidenceTypeID = createDto.EvidenceTypeID,
                FK_EvidenceLocationID = createDto.EvidenceLocationID,
                FK_InvestigationID = docket.FK_InvestigationID ?? 0,
                FK_DocketID = createDto.DocketID,
                FK_UserID = registeredByUserId
            };

            var initialCustody = new EvidenceTransfer
            {
                FromLocationID = createDto.EvidenceLocationID,
                ToLocationID = createDto.EvidenceLocationID,
                TransferredAt = DateTime.UtcNow,
                TransferredByUserID = registeredByUserId,
                ReceivingOfficerUserID = registeredByUserId,
                SignatureMethod = createDto.SignatureMethod,
                PayloadHash = payloadHash,
                TransferReason = "Initial evidence registration"
            };
            await _evidenceRepository.CreateWithInitialCustodyAsync(evidence, initialCustody);

            var response = new EvidenceResponseDto
            {
                EvidenceID = evidence.EvidenceID,
                EvidenceReferenceNumber = evidence.EvidenceReferenceNumber,
                Description = evidence.Description,
                EvidenceType = evidence.EvidenceType?.Name,
                Quantity = evidence.Quantity,
                DateCollected = evidence.DateCollected,
                CollectionMethod = evidence.CollectionMethod,
                StateOfItem = evidence.StateOfItem,
                EvidenceLocation = evidence.EvidenceLocation?.LocationName,
                EvidenceLocationID = evidence.FK_EvidenceLocationID,
                SignatureMethod = evidence.SignatureMethod,
                PayloadHash = evidence.PayloadHash,
                FileHash = evidence.FileHash,
                ImageUrl = evidence.ImagePath != null ? $"/api/files/evidence/{evidence.EvidenceID}" : null
            };

            await _auditService.LogActionAsync("CREATE_EVIDENCE", "Evidence", evidence.EvidenceID, null, response, registeredByUserId);

            return ServiceResult<EvidenceResponseDto>.SuccessResult(response, "Evidence item registered successfully.");
        }

        private static string ComputeEvidencePayloadHash(CreateEvidenceDto dto, string? fileHash)
        {
            var canonicalPayload = string.Join("|", new[]
            {
                dto.DocketID.ToString(),
                dto.EvidenceTypeID.ToString(),
                dto.Quantity.ToString(),
                dto.DateCollected.ToUniversalTime().ToString("O"),
                dto.Description?.Trim() ?? string.Empty,
                dto.CollectionMethod?.Trim() ?? string.Empty,
                dto.StateOfItem?.Trim() ?? string.Empty,
                dto.EvidenceLocationID.ToString(),
                dto.SignatureMethod?.Trim() ?? string.Empty,
                fileHash ?? string.Empty
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload))).ToLowerInvariant();
        }

        // UC 5.2 - Read Evidence Item by ID
        public async Task<ServiceResult<EvidenceResponseDto>> GetEvidenceByIdAsync(int evidenceId, int requestingUserId)
        {
            var evidence = await _evidenceRepository.GetByIdAsync(evidenceId);
            if (evidence == null)
                return ServiceResult<EvidenceResponseDto>.Failure("Evidence not found.");

            var requestingUser = await _userRepository.GetByIdAsync(requestingUserId);
            if (requestingUser == null)
                return ServiceResult<EvidenceResponseDto>.Failure("User not found.");

            var docket = await _docketRepository.GetByIdAsync(evidence.FK_DocketID);
            if (docket.FK_StationID != requestingUser.FK_StationID && requestingUser.Role?.Name != "System Administrator")
                return ServiceResult<EvidenceResponseDto>.Failure("You do not have permission to view this evidence.");

            // Chain of custody removed for I5
            // var chainOfCustody = await _evidenceRepository.GetChainOfCustodyAsync(evidenceId);

            var response = new EvidenceResponseDto
            {
                EvidenceID = evidence.EvidenceID,
                EvidenceReferenceNumber = evidence.EvidenceReferenceNumber,
                Description = evidence.Description,
                EvidenceType = evidence.EvidenceType?.Name,
                Quantity = evidence.Quantity,
                DateCollected = evidence.DateCollected,
                CollectionMethod = evidence.CollectionMethod,
                StateOfItem = evidence.StateOfItem,
                EvidenceLocation = evidence.EvidenceLocation?.LocationName,
                EvidenceLocationID = evidence.FK_EvidenceLocationID,
                SignatureMethod = evidence.SignatureMethod,
                PayloadHash = evidence.PayloadHash,
                FileHash = evidence.FileHash,
                ImageUrl = evidence.ImagePath != null ? $"/api/files/evidence/{evidence.EvidenceID}" : null
                // ChainOfCustodyHistory removed for I5
            };

            await _auditService.LogActionAsync("READ_EVIDENCE", "Evidence", evidenceId, null, null, requestingUserId);

            return ServiceResult<EvidenceResponseDto>.SuccessResult(response);
        }

        public async Task<ServiceResult<List<EvidenceResponseDto>>> GetEvidenceByDocketAsync(int docketId, int requestingUserId)
        {
            var requestingUser = await _userRepository.GetByIdAsync(requestingUserId);
            if (requestingUser == null)
                return ServiceResult<List<EvidenceResponseDto>>.Failure("User not found.");

            var docket = await _docketRepository.GetByIdAsync(docketId);
            if (docket == null)
                return ServiceResult<List<EvidenceResponseDto>>.Failure("Docket not found.");

            if (docket.FK_StationID != requestingUser.FK_StationID && requestingUser.Role?.Name != "System Administrator")
                return ServiceResult<List<EvidenceResponseDto>>.Failure("You do not have permission to view evidence for this docket.");

            var evidenceList = await _evidenceRepository.GetByDocketIdAsync(docketId);

            var response = evidenceList.Select(e => new EvidenceResponseDto
            {
                EvidenceID = e.EvidenceID,
                EvidenceReferenceNumber = e.EvidenceReferenceNumber,
                Description = e.Description,
                EvidenceType = e.EvidenceType?.Name,
                Quantity = e.Quantity,
                DateCollected = e.DateCollected,
                CollectionMethod = e.CollectionMethod,
                StateOfItem = e.StateOfItem,
                EvidenceLocation = e.EvidenceLocation?.LocationName,
                EvidenceLocationID = e.FK_EvidenceLocationID,
                SignatureMethod = e.SignatureMethod,
                PayloadHash = e.PayloadHash,
                FileHash = e.FileHash,
                ImageUrl = e.ImagePath != null ? $"/api/files/evidence/{e.EvidenceID}" : null
            }).ToList();

            return ServiceResult<List<EvidenceResponseDto>>.SuccessResult(response);
        }

        // UC 5.3 - Update Evidence Item
        public async Task<ServiceResult<bool>> UpdateEvidenceAsync(int evidenceId, UpdateEvidenceDto updateDto, int requestingUserId)
        {
            if (!TryValidate(updateDto, out var validationError))
                return ServiceResult<bool>.Failure(validationError);

            var requestingUser = await _userRepository.GetByIdAsync(requestingUserId);
            if (requestingUser == null)
                return ServiceResult<bool>.Failure("User not found.");

            var evidence = await _evidenceRepository.GetByIdAsync(evidenceId);
            if (evidence == null)
                return ServiceResult<bool>.Failure("Evidence not found.");

            var docket = await _docketRepository.GetByIdAsync(evidence.FK_DocketID);
            if (docket == null)
                return ServiceResult<bool>.Failure("Docket not found.");

            var isSystemAdmin = requestingUser.Role?.Name == "System Administrator";
            if (!isSystemAdmin && docket.FK_StationID != requestingUser.FK_StationID)
                return ServiceResult<bool>.Failure("You do not have permission to update this evidence.");

            if (docket.DocketStatus?.StatusName is "CLOSED" or "ARCHIVED")
                return ServiceResult<bool>.Failure("Evidence on a closed or archived docket is read-only.");

            var isAssigned = await _evidenceRepository.IsUserAssignedToDocketAsync(requestingUserId, evidence.FK_DocketID);
            var isDocketAdmin = requestingUser.Role?.Name == "Docket Administrator";

            if (!isAssigned && !isDocketAdmin && !isSystemAdmin)
                return ServiceResult<bool>.Failure("Only the assigned Investigating Officer or Docket Administrator may update this evidence.");

            if (string.IsNullOrWhiteSpace(updateDto.ReasonForUpdate) || updateDto.ReasonForUpdate.Length < 10)
                return ServiceResult<bool>.Failure("A reason of at least 10 characters is required for evidence updates.");

            if (updateDto.Quantity <= 0)
                return ServiceResult<bool>.Failure("Quantity must be greater than zero.");

            var evidenceLocations = await _evidenceRepository.GetEvidenceLocationsAsync();
            if (!evidenceLocations.Any(location => location.EvidenceLocationID == updateDto.EvidenceLocationID))
                return ServiceResult<bool>.Failure("The selected evidence location does not exist.");

            if (!string.IsNullOrWhiteSpace(updateDto.CollectionMethod) &&
                !OperationalLookupCatalog.ContainsIgnoreCase(
                    OperationalLookupCatalog.EvidenceCollectionMethods,
                    updateDto.CollectionMethod))
                return ServiceResult<bool>.Failure("Select a valid evidence collection method from the provided list.");

            if (!string.IsNullOrWhiteSpace(updateDto.StateOfItem) &&
                !OperationalLookupCatalog.ContainsIgnoreCase(
                    OperationalLookupCatalog.EvidenceItemStates,
                    updateDto.StateOfItem))
                return ServiceResult<bool>.Failure("Select a valid state of item from the provided list.");

            if (!IsValidSignatureMethod(updateDto.SignatureMethod))
                return ServiceResult<bool>.Failure("Select Ink, Stylus or Smartcard as the signature method.");

            if (updateDto.EvidenceLocationID != evidence.FK_EvidenceLocationID)
                return ServiceResult<bool>.Failure("Use Record Transfer to change an evidence storage location so chain of custody is preserved.");

            var oldValues = new
            {
                evidence.Description,
                evidence.Quantity,
                evidence.CollectionMethod,
                evidence.StateOfItem,
                evidence.FK_EvidenceLocationID,
                evidence.SignatureMethod,
                evidence.PayloadHash
            };

            evidence.Description = updateDto.Description!.Trim();
            evidence.Quantity = updateDto.Quantity;
            evidence.CollectionMethod = updateDto.CollectionMethod!.Trim();
            evidence.StateOfItem = updateDto.StateOfItem!.Trim();
            evidence.SignatureMethod = updateDto.SignatureMethod!.Trim();
            // PayloadHash is an immutable system-generated integrity value.

            await _evidenceRepository.UpdateAsync(evidence);

            var newValues = new
            {
                evidence.Description,
                evidence.Quantity,
                evidence.CollectionMethod,
                evidence.StateOfItem,
                evidence.FK_EvidenceLocationID,
                evidence.SignatureMethod,
                evidence.PayloadHash
            };

            await _auditService.LogActionAsync("UPDATE_EVIDENCE", "Evidence", evidenceId, oldValues, newValues, requestingUserId, updateDto.ReasonForUpdate);

            return ServiceResult<bool>.SuccessResult(true, "Evidence item updated successfully.");
        }

        public async Task<ServiceResult<List<EvidenceType>>> GetEvidenceTypesAsync()
        {
            var types = await _evidenceRepository.GetEvidenceTypesAsync();
            return ServiceResult<List<EvidenceType>>.SuccessResult(types);
        }

        public async Task<ServiceResult<List<EvidenceLocation>>> GetEvidenceLocationsAsync()
        {
            var locations = await _evidenceRepository.GetEvidenceLocationsAsync();
            return ServiceResult<List<EvidenceLocation>>.SuccessResult(locations);
        }

        private static bool TryValidate(object dto, out string error)
        {
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(dto);
            var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                dto,
                context,
                results,
                validateAllProperties: true);
            error = results.FirstOrDefault()?.ErrorMessage ?? "The supplied evidence details are invalid.";
            return valid;
        }

        private static bool IsValidSignatureMethod(string? value) =>
            new[] { "Ink", "Stylus", "Smartcard" }
                .Any(method => string.Equals(method, value?.Trim(), StringComparison.OrdinalIgnoreCase));

        private static string? NormalizeOptional(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
