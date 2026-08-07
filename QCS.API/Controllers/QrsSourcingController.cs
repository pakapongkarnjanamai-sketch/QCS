using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Abstractions;

namespace QCS.API.Controllers
{
    [Route("api/QrsSourcing")]
    [ApiController]
    [Authorize(Policy = "DomainUser")]
    public sealed class QrsSourcingController : ControllerBase
    {
        private readonly IQrsSourcingService _qrsSourcingService;
        private readonly ILogger<QrsSourcingController> _logger;

        public QrsSourcingController(
            IQrsSourcingService qrsSourcingService,
            ILogger<QrsSourcingController> logger)
        {
            _qrsSourcingService = qrsSourcingService;
            _logger = logger;
        }

        [HttpGet("Requests")]
        public async Task<IActionResult> Search(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? intent = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _qrsSourcingService.GetRequestsAsync(search, page, pageSize, intent, cancellationToken);
                return Ok(result);
            }
            catch (QrsSourcingException ex)
            {
                return MapQrsException(ex);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QRS sourcing search failed.");
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "QRS sourcing lookup is unavailable.");
            }
        }

        [HttpGet("Requests/{code}")]
        public async Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _qrsSourcingService.GetByCodeAsync(code, cancellationToken);
                if (result == null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Document not found",
                        detail: $"QRS request '{code}' not found.");
                }

                return Ok(result);
            }
            catch (QrsSourcingException ex)
            {
                return MapQrsException(ex);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QRS sourcing get-by-code failed.");
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "QRS sourcing lookup is unavailable.");
            }
        }

        private IActionResult MapQrsException(QrsSourcingException ex)
        {
            _logger.LogWarning("QRS sourcing exception: {Message}, status: {StatusCode}", ex.Message, ex.StatusCode);

            var statusCode = ex.IsContractViolation
                ? StatusCodes.Status502BadGateway
                : ex.StatusCode is StatusCodes.Status503ServiceUnavailable or StatusCodes.Status504GatewayTimeout || ex.StatusCode == null
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status502BadGateway;

            return Problem(
                statusCode: statusCode,
                title: "QRS sourcing lookup is unavailable.");
        }
    }
}