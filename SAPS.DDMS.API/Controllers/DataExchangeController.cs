using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SAPS.DDMS.API.Models.DTOs;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/data-exchange")]
    [Authorize(Policy = PermissionCatalog.DataExchangeManage)]
    public sealed class DataExchangeController : ControllerBase
    {
        private readonly IDataExchangeService _dataExchange;
        private readonly IAuditService _audit;

        public DataExchangeController(
            IDataExchangeService dataExchange,
            IAuditService audit)
        {
            _dataExchange = dataExchange;
            _audit = audit;
        }

        [HttpGet("export/{dataset}")]
        public async Task<IActionResult> Export(
            string dataset,
            [FromQuery] string format = "xlsx",
            [FromQuery] DataExchangeFilterDto? filters = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = GetCurrentUserId();
                var export = await _dataExchange.ExportAsync(
                    dataset,
                    format,
                    filters ?? new DataExchangeFilterDto(),
                    userId,
                    cancellationToken);

                await _audit.LogActionAsync(
                    "EXPORT_DATA_FILE",
                    "DataExchange",
                    null,
                    null,
                    new
                    {
                        export.Dataset,
                        export.Format,
                        export.RowCount,
                        export.FileName
                    },
                    userId,
                    "Administrator exported parameterised operational data.",
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                return File(export.Content, export.ContentType, export.FileName);
            }
            catch (DataExchangeValidationException exception)
            {
                return BadRequest(new { success = false, message = exception.Message });
            }
        }

        [HttpPost("lookups/import/preview")]
        [RequestSizeLimit(1_048_576)]
        public async Task<IActionResult> PreviewLookupImport(
            [FromBody] LookupImportDocumentDto document,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var preview = await _dataExchange.PreviewLookupImportAsync(
                    document,
                    GetCurrentUserId(),
                    cancellationToken);
                return Ok(preview);
            }
            catch (DataExchangeValidationException exception)
            {
                return BadRequest(new { success = false, message = exception.Message });
            }
        }

        [HttpPost("lookups/import/apply")]
        [RequestSizeLimit(1_048_576)]
        public async Task<IActionResult> ApplyLookupImport(
            [FromBody] LookupImportDocumentDto document,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _dataExchange.ApplyLookupImportAsync(
                    document,
                    GetCurrentUserId(),
                    cancellationToken);
                return Ok(result);
            }
            catch (DataExchangeValidationException exception)
            {
                return BadRequest(new { success = false, message = exception.Message });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdValue = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
                throw new UnauthorizedAccessException("User id claim not found.");
            return userId;
        }
    }
}
