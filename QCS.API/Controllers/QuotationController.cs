using Microsoft.AspNetCore.Authorization;
using QCS.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Services;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QuotationController : ControllerBase
    {
        private readonly IRequestService _requestService;
        private readonly IQuotationService _quotationService;

        public QuotationController(
            IRequestService requestService,
            IQuotationService quotationService)
        {
            _requestService = requestService;
            _quotationService = quotationService;
        }

        [HttpGet("ByCode")]
        [HttpGet("ByCode/{code}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByCode([FromRoute(Name = "code")] string? routeCode, [FromQuery(Name = "code")] string? queryCode, CancellationToken cancellationToken)
        {
            var resolvedCode = string.IsNullOrWhiteSpace(routeCode) ? queryCode : routeCode;
            return await GetByCodeCore(resolvedCode);
        }

        private async Task<IActionResult> GetByCodeCore(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request",
                    detail: "Query parameter 'code' is required.");
            }

            var result = await _requestService.GetByCodeAsync(code);

            if (result == null)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document not found",
                    detail: "ไม่พบข้อมูลเอกสาร");
            }

            return Ok(result);
        }

        [HttpGet("ViewFile/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ViewFile(int id, CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request",
                    detail: "Route parameter 'id' must be greater than 0.");
            }

            try
            {
                var fileDto = await _quotationService.GenerateStampedPdfAsync(id, cancellationToken);

                if (fileDto?.Data == null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "File not found",
                        detail: "File content missing");
                }

                return File(fileDto.Data, fileDto.ContentType, fileDto.FileName);
            }
            catch (KeyNotFoundException)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document not found",
                    detail: "ไม่พบข้อมูลเอกสาร");
            }
            catch (PdfServiceException ex)
            {
                var statusCode = ex.UpstreamStatusCode == StatusCodes.Status504GatewayTimeout
                    || ex.UpstreamStatusCode == StatusCodes.Status408RequestTimeout
                    ? StatusCodes.Status504GatewayTimeout
                    : StatusCodes.Status502BadGateway;

                return Problem(
                    statusCode: statusCode,
                    title: statusCode == StatusCodes.Status504GatewayTimeout
                        ? "PDF service timeout"
                        : "PDF service unavailable",
                    detail: ex.Message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client cancelled request while PDF was being prepared.
                return StatusCode(499);
            }
            catch (InvalidOperationException)
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Server configuration error",
                    detail: "PDF service configuration is invalid.");
            }
        }
    }
}