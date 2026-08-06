using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Services;
using QCS.Domain.DTOs;
using QCS.Domain.DTOs.Portal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QCS.API.Controllers
{
    [Route("api/Portal/Requests")]
    [ApiController]
    [Authorize(Policy = "DomainUser")]
    public class PortalRequestsController : ControllerBase
    {
        private readonly IRequestService _requestService;
        private readonly IQuotationService _quotationService;

        public PortalRequestsController(IRequestService requestService, IQuotationService quotationService)
        {
            _requestService = requestService;
            _quotationService = quotationService;
        }

        [HttpGet]
        public async Task<ActionResult<PortalPage<PortalRequestListItemDto>>> GetRequests(
            [FromQuery] PortalRequestQuery query,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query.View) ||
                !Enum.TryParse<PortalRequestView>(query.View, ignoreCase: true, out _))
            {
                return Problem(
                    detail: $"Invalid view '{query.View}'. Valid values are: {nameof(PortalRequestView.MyTasks)}, {nameof(PortalRequestView.MyRequests)}, {nameof(PortalRequestView.MyApproved)}, {nameof(PortalRequestView.Rejected)}, {nameof(PortalRequestView.AllApproved)}.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request");
            }

            var result = await _requestService.GetPortalRequestsAsync(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(PortalRequestDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRequestById([FromRoute] int id, CancellationToken cancellationToken)
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
                var result = await _requestService.GetPortalRequestByIdAsync(id, cancellationToken);
                if (result == null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Document not found",
                        detail: "Request document not found.");
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: "You do not have permission to view this request.");
            }
        }

        [HttpGet("by-code/{code}")]
        [ProducesResponseType(typeof(PortalRequestDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRequestByCode([FromRoute] string code, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request",
                    detail: "Route parameter 'code' is required.");
            }

            try
            {
                var result = await _requestService.GetPortalRequestByCodeAsync(code, cancellationToken);
                if (result == null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Document not found",
                        detail: "Request document not found.");
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: "You do not have permission to view this request.");
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(PortalSaveResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateDraft([FromBody] SavePortalRequestDto input, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _requestService.CreatePortalDraftAsync(input, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request",
                    detail: ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(PortalSaveResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateDraft([FromRoute] int id, [FromBody] SavePortalRequestDto input, CancellationToken cancellationToken)
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
                var result = await _requestService.UpdatePortalDraftAsync(id, input, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document not found",
                    detail: ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid operation",
                    detail: ex.Message);
            }
        }

        [HttpPost("{id:int}/submit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Submit([FromRoute] int id, CancellationToken cancellationToken)
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
                await _requestService.SubmitPortalRequestAsync(id, cancellationToken);
                return Ok(new { success = true, message = "Submitted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document not found",
                    detail: ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Submit validation failed",
                    detail: ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDraft([FromRoute] int id, CancellationToken cancellationToken)
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
                await _requestService.DeletePortalDraftAsync(id, cancellationToken);
                return Ok(new { success = true, message = "Deleted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document not found",
                    detail: ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid operation",
                    detail: ex.Message);
            }
        }

        [HttpPost("{id:int}/attachments")]
        [ProducesResponseType(typeof(PortalAttachmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddAttachment([FromRoute] int id, [FromForm] UploadPortalAttachmentDto input, CancellationToken cancellationToken)
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
                var result = await _requestService.AddPortalAttachmentAsync(id, input, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document not found",
                    detail: ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid operation",
                    detail: ex.Message);
            }
        }

        [HttpDelete("{id:int}/attachments/{attachmentId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAttachment([FromRoute] int id, [FromRoute] int attachmentId, CancellationToken cancellationToken)
        {
            if (id <= 0 || attachmentId <= 0)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request",
                    detail: "Route parameters 'id' and 'attachmentId' must be greater than 0.");
            }

            try
            {
                await _requestService.DeletePortalAttachmentAsync(id, attachmentId, cancellationToken);
                return Ok(new { success = true, message = "Attachment deleted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Attachment not found",
                    detail: ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid operation",
                    detail: ex.Message);
            }
        }

        [HttpPost("{id:int}/preview")]
        public async Task<IActionResult> PreviewMergedPdf([FromRoute] int id, CancellationToken cancellationToken)
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
                var detail = await _requestService.GetPortalRequestByIdAsync(id, cancellationToken);
                if (detail == null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Document not found",
                        detail: "Request document not found.");
                }

                var userDocs = detail.Documents.Where(d => d.DocumentTypeId != 99).ToList();
                if (userDocs.Count == 0)
                {
                    return Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "No attachments",
                        detail: "Request has no attached files to preview.");
                }

                var pdfFiles = new List<PdfFileDto>();
                foreach (var doc in userDocs)
                {
                    var attachment = await _requestService.GetAttachmentAsync(doc.Id);
                    if (attachment?.Data != null)
                    {
                        pdfFiles.Add(new PdfFileDto
                        {
                            Name = attachment.FileName,
                            DocumentTypeId = doc.DocumentTypeId <= 0 ? 10 : doc.DocumentTypeId,
                            ContentType = attachment.ContentType ?? "application/pdf",
                            Data = attachment.Data,
                            Length = attachment.Data.LongLength
                        });
                    }
                }

                if (pdfFiles.Count == 0)
                {
                    return Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "No previewable files",
                        detail: "No valid attachment files found for preview.");
                }

                var approvalSteps = detail.WorkflowSteps
                    .Where(step => step.ActionDate.HasValue && !string.IsNullOrWhiteSpace(step.ApproverName))
                    .Select(step => new StepDto
                    {
                        StepName = step.StepName ?? "Step",
                        Approver = step.ApproverName!,
                        ApprovalDate = step.ActionDate!.Value
                    }).ToList();

                var previewRequest = new MergeAndStampRequestDto
                {
                    DocumentName = detail.Title,
                    ReferenceCode = detail.Code,
                    PdfFiles = pdfFiles,
                    ApprovalData = new ApprovalDataDto
                    {
                        Name = detail.Title,
                        Step = approvalSteps
                    },
                    DrawSetting = new DrawSettingDto
                    {
                        Color = "#000000",
                        FontSize = 8,
                        Margin = 20,
                        AlignmentStamp = 2
                    }
                };

                var previewFile = await _quotationService.GeneratePreviewMergedPdfAsync(previewRequest, $"Preview_{detail.Code}", cancellationToken);
                if (previewFile.Data == null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Preview generation failed",
                        detail: "Unable to generate preview PDF.");
                }

                return File(previewFile.Data, previewFile.ContentType, previewFile.FileName);
            }
            catch (UnauthorizedAccessException)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: "You do not have permission to preview this request.");
            }
            catch (PdfServiceException ex)
            {
                var statusCode = ex.UpstreamStatusCode == StatusCodes.Status504GatewayTimeout || ex.UpstreamStatusCode == StatusCodes.Status408RequestTimeout
                    ? StatusCodes.Status504GatewayTimeout
                    : StatusCodes.Status502BadGateway;

                return Problem(
                    statusCode: statusCode,
                    title: statusCode == StatusCodes.Status504GatewayTimeout ? "PDF service timeout" : "PDF service unavailable",
                    detail: ex.Message);
            }
        }

        [HttpPost("{id:int}/approve")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Approve([FromRoute] int id, [FromBody] PortalApprovalActionDto input, CancellationToken cancellationToken)
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
                await _requestService.ApprovePortalRequestAsync(id, input, cancellationToken);
                return Ok(new { success = true, message = "Approved successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document not found",
                    detail: ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid operation",
                    detail: ex.Message);
            }
        }

        [HttpPost("{id:int}/reject")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Reject([FromRoute] int id, [FromBody] PortalApprovalActionDto input, CancellationToken cancellationToken)
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
                await _requestService.RejectPortalRequestAsync(id, input, cancellationToken);
                return Ok(new { success = true, message = "Rejected successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document not found",
                    detail: ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid operation",
                    detail: ex.Message);
            }
        }

        [HttpPost("{id:int}/return")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Return([FromRoute] int id, [FromBody] PortalApprovalActionDto input, CancellationToken cancellationToken)
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
                await _requestService.ReturnPortalRequestAsync(id, input, cancellationToken);
                return Ok(new { success = true, message = "Returned successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document not found",
                    detail: ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid operation",
                    detail: ex.Message);
            }
        }

        [HttpPost("{id:int}/cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Cancel([FromRoute] int id, [FromBody] PortalApprovalActionDto input, CancellationToken cancellationToken)
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
                await _requestService.CancelPortalRequestAsync(id, input, cancellationToken);
                return Ok(new { success = true, message = "Cancelled successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document not found",
                    detail: ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid operation",
                    detail: ex.Message);
            }
        }

        [HttpPost("route-preview")]
        [ProducesResponseType(typeof(QCS.Application.Abstractions.ApprovalPreviewResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RoutePreview([FromBody] SavePortalRequestDto input, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _requestService.GetRoutePreviewAsync(input, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Route preview failed",
                    detail: ex.Message);
            }
        }
    }
}
