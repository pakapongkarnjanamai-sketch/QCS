using Microsoft.AspNetCore.Authorization;
using QCS.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Services;
using QCS.Domain.DTOs;

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

        // GET api/Quotation/GetEffectiveByVendorCodeAsync?code=V001
        [HttpGet("GetEffectiveByVendorCodeAsync")]
        [ProducesResponseType(typeof(List<QuotationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetEffectiveByVendorCode([FromQuery] string code, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request",
                    detail: "Query parameter 'code' is required.");
            }

            var result = await _quotationService.GetEffectiveByVendorCodeAsync(code, cancellationToken);
            return Ok(result);
        }

        // GET api/Quotation/GetEffective?vendorCode=&keyword=&page=1&pageSize=20&sortBy=RequestDate&sortDescending=true
        [HttpGet("GetEffective")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<QuotationDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<QuotationDto>>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetEffective([FromQuery] EffectiveQuotationQuery query, CancellationToken cancellationToken)
        {
            if (query.RequestDateFrom.HasValue && query.RequestDateTo.HasValue
                && query.RequestDateFrom.Value.Date > query.RequestDateTo.Value.Date)
            {
                return BadRequest(ApiResponse<PagedResult<QuotationDto>>.Fail(
                    StatusCodes.Status400BadRequest,
                    "RequestDateFrom must not be after RequestDateTo."));
            }

            var paged = await _quotationService.GetEffectiveAsync(query, cancellationToken);
            return Ok(ApiResponse<PagedResult<QuotationDto>>.Ok(paged));
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

                Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                Response.Headers.Pragma = "no-cache";
                Response.Headers.Expires = "0";

                // Inline, for the same reason as Request/ViewFile: a file name passed to File()
                // sets Content-Disposition: attachment and the stamped PDF downloads instead of
                // previewing. The name is kept on the header so "save as" still offers it.
                //
                // SetHttpFileName, not System.Net.Mime.ContentDisposition: that one encodes a
                // non-ASCII name as an RFC 2047 word (=?utf-8?B?...?=) folded across a line break,
                // which browsers do not decode in this header — a Thai file name would arrive as
                // mojibake. This emits an ASCII fallback plus the RFC 5987 filename*.
                var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline");
                disposition.SetHttpFileName(fileDto.FileName);
                Response.Headers.ContentDisposition = disposition.ToString();

                return File(fileDto.Data, fileDto.ContentType);
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