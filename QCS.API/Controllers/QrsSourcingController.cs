using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QCS.API.Integration;

namespace QCS.API.Controllers
{
    [Route("api/QrsSourcing")]
    [ApiController]
    [Authorize(Policy = "DomainUser")]
    public sealed class QrsSourcingController : ControllerBase
    {
        private readonly IQrsSourcingClient _qrsSourcingClient;
        private readonly ILogger<QrsSourcingController> _logger;

        public QrsSourcingController(
            IQrsSourcingClient qrsSourcingClient,
            ILogger<QrsSourcingController> logger)
        {
            _qrsSourcingClient = qrsSourcingClient;
            _logger = logger;
        }

        [HttpGet("Requests")]
        public Task<IActionResult> Search([FromQuery] string? search, CancellationToken cancellationToken) =>
            ForwardAsync(() => _qrsSourcingClient.SearchAsync(search, cancellationToken), cancellationToken);

        [HttpGet("Requests/{code}")]
        public Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken) =>
            ForwardAsync(() => _qrsSourcingClient.GetByCodeAsync(code, cancellationToken), cancellationToken);

        private async Task<IActionResult> ForwardAsync(
            Func<Task<HttpResponseMessage>> sendAsync,
            CancellationToken cancellationToken)
        {
            try
            {
                using var response = await sendAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "QRS sourcing lookup returned upstream status code {StatusCode}.",
                        (int)response.StatusCode);

                    var statusCode = response.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                        or System.Net.HttpStatusCode.GatewayTimeout
                        ? StatusCodes.Status503ServiceUnavailable
                        : StatusCodes.Status502BadGateway;

                    return Problem(
                        statusCode: statusCode,
                        title: "QRS sourcing lookup is unavailable.");
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = body,
                    ContentType = "application/json"
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "QRS sourcing lookup failed.");
                return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "QRS sourcing lookup is unavailable.");
            }
        }
    }
}