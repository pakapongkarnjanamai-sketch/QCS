using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QCS.Application.Abstractions;
using QCS.API.Authentication;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Services;
using QCS.Domain.DTOs;
using QCS.Domain.DTOs.Integration;
using QCS.Domain.DTOs.Portal;

namespace QCS.API.Controllers
{
    /// <summary>
    /// Controller สำหรับให้โปรแกรมภายนอกเชื่อมต่อเพื่อดึงข้อมูล
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class IntegrationController : ControllerBase
    {
        private readonly IRequestService _requestService;
        private readonly IDateTime  _dateTime;
        public IntegrationController(IRequestService requestService, IDateTime dateTime)
        {
            _requestService = requestService;
            _dateTime = dateTime;
        }

        /// <summary>
        /// 1. GetRequestAll: ดึงข้อมูล List ของ Request ที่เป็น Status อนุมัติครบถ้วน
        /// </summary>
        /// <returns>List of Approved Requests</returns>
        [HttpGet("GetRequestAll")]
        [Authorize(Policy = "DomainUser")]
        public async Task<ActionResult<List<RequestGridDto>>> GetRequestAll()
        {
            try
            {
                // เรียกใช้ Query จาก Service เดิมที่มีอยู่แล้ว (GetApprovedListQuery) 
                // ซึ่งมีการ Filter Status = Approved (2) ไว้ให้แล้วใน Service
                var query = _requestService.GetApprovedListQuery();

                var result = await query.Where(a=>a.ValidUntil >= _dateTime.Now).ToListAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// 2. GetRequestByVendorCode: ดึงข้อมูล List ของ Request ที่เป็น Status อนุมัติครบถ้วน โดยหาจาก VendorCode
        /// </summary>
        /// <param name="vendorCode">รหัส Vendor ที่ต้องการค้นหา</param>
        /// <returns>List of Approved Requests filtered by VendorCode</returns>
        [HttpGet("GetRequestByVendorCode")]
        [Authorize(Policy = "DomainUser")]
        public async Task<ActionResult<List<RequestGridDto>>> GetRequestByVendorCode(string vendorCode)
        {
            if (string.IsNullOrWhiteSpace(vendorCode))
            {
                return BadRequest("VendorCode is required.");
            }

            try
            {
                // ใช้ Query เดิมและเพิ่มเงื่อนไขการกรอง VendorCode เข้าไป
                var query = _requestService.GetApprovedListQuery();

                var result = await query
                    .Where(r => r.VendorCode == vendorCode && r.ValidUntil >= _dateTime.Now)
                    .ToListAsync();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("GetRequestsBySource")]
        [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName, Policy = "IntegrationClient")]
        public async Task<ActionResult<List<SourcedRequestDto>>> GetRequestsBySource(
            [FromQuery] string system,
            [FromQuery] string number)
        {
            if (string.IsNullOrWhiteSpace(system) || string.IsNullOrWhiteSpace(number))
            {
                return BadRequest("System and number are required.");
            }

            var result = await _requestService
                .GetBySourceQuery(system.Trim(), number.Trim())
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("RenewalCandidates")]
        [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName, Policy = "IntegrationClient")]
        public async Task<ActionResult<PortalPage<IntegrationRenewalCandidateDto>>> GetRenewalCandidates(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await _requestService.GetIntegrationRenewalCandidatesAsync(search, page, pageSize, cancellationToken);
            return Ok(result);
        }

        [HttpGet("RenewalCandidates/{code}")]
        [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName, Policy = "IntegrationClient")]
        public async Task<ActionResult<IntegrationRenewalCandidateDto>> GetRenewalCandidateByCode(
            [FromRoute] string code,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest("Code is required.");
            }

            var result = await _requestService.GetIntegrationRenewalCandidateByCodeAsync(code, cancellationToken);
            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpGet("Requests/{qcCode}/Sources/QRS/{qrsCode}/Documents/{documentId}")]
        [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName, Policy = "IntegrationClient")]
        public async Task<IActionResult> GetSourcedDocument(
            [FromRoute] string qcCode,
            [FromRoute] string qrsCode,
            [FromRoute] int documentId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(qcCode) || string.IsNullOrWhiteSpace(qrsCode) || documentId <= 0)
            {
                return NotFound();
            }

            var document = await _requestService.GetSourcedDocumentAsync(qcCode, qrsCode, documentId, cancellationToken);
            if (document == null || document.Content == null || document.Content.Length == 0)
            {
                return NotFound();
            }

            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";

            var contentType = document.ContentType ?? "application/octet-stream";
            if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline");
                disposition.SetHttpFileName(document.FileName);
                Response.Headers.ContentDisposition = disposition.ToString();
                return File(document.Content, contentType);
            }

            return File(document.Content, contentType, document.FileName);
        }
    }
}