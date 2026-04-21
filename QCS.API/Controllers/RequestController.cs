using DevExtreme.AspNet.Data;
using QCS.Application.Abstractions;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Services;
using QCS.Domain.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService _service;
        private readonly IQuotationService _quotationService;

        public RequestController(IRequestService service, IQuotationService quotationService)
        {
            _service = service;
            _quotationService = quotationService;
        }

        private static object LoadGrid(IQueryable<RequestGridDto> query, DataSourceLoadOptions loadOptions)
        {
            return DataSourceLoader.Load(query, loadOptions);
        }

        // ==========================================================
        // ⚡ DATA GRID ENDPOINTS
        // ==========================================================

        [HttpGet("MyRequests")]
        public object GetMyRequests(DataSourceLoadOptions loadOptions)
        {
            return LoadGrid(_service.GetMyRequestsQuery(), loadOptions);
        }

        [HttpGet("MyTasks")]
        public async Task<object> GetMyTasks(DataSourceLoadOptions loadOptions)
        {
            return LoadGrid(await _service.GetMyTasksQueryAsync(), loadOptions);
        }

        [HttpGet("Approved")]
        public object GetApprovedList(DataSourceLoadOptions loadOptions)
        {
            return LoadGrid(_service.GetApprovedListQuery(), loadOptions);
        }

        [HttpGet("MyApproved")]
        public object GetMyApprovedList(DataSourceLoadOptions loadOptions)
        {
            return LoadGrid(_service.GetMyApprovedListQuery(), loadOptions);
        }

        // ==========================================================
        // 📥 Detail & Actions
        // ==========================================================

        [HttpGet("Detail/{id}")]
        public async Task<IActionResult> GetRequestDetail(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound("ไม่พบข้อมูลเอกสาร");
            return Ok(result);
        }

        [HttpGet("ByCode/{code}")]
        public async Task<IActionResult> GetRequestDetailByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request",
                    detail: "Route parameter 'code' is required.");
            }

            var result = await _service.GetByCodeAsync(code);
            if (result == null) return NotFound("ไม่พบข้อมูลเอกสาร");
            return Ok(result);
        }

        [HttpGet("ByCode")]
        public async Task<IActionResult> GetRequestDetailByCodeQuery([FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request",
                    detail: "Query parameter 'code' is required.");
            }

            var result = await _service.GetByCodeAsync(code);
            if (result == null) return NotFound("ไม่พบข้อมูลเอกสาร");
            return Ok(result);
        }
        [HttpPost("Save")] // บันทึกเป็น Draft
        public async Task<IActionResult> Save([FromForm] CreateRequestDto input)
        {
            // ส่ง flag isSubmit = false ไปให้ Service
            var result = await _service.CreateAsync(input, isSubmit: false);
            return Ok(new { success = true, id = result.Id, docNo = result.Code });
        }
        [HttpPost("Submit")]
        public async Task<IActionResult> Submit([FromForm] CreateRequestDto input)
        {
         
            await _service.CreateAsync(input,  isSubmit: true);
            return Ok(new { success = true });
        }

        [HttpPost("Update")]
        public async Task<IActionResult> Update([FromForm] UpdateRequestDto input)
        {
            await _service.UpdateAsync(input, isSubmit: false);
            return Ok(new { success = true });
        }

        [HttpPost("SubmitUpdate")]
        public async Task<IActionResult> SubmitUpdate([FromForm] UpdateRequestDto input)
        {
            await _service.UpdateAsync(input, isSubmit: true);
            return Ok(new { success = true });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return Ok(new { message = "Deleted successfully" });
        }

        [HttpGet("ViewFile/{id}")]
        public async Task<IActionResult> ViewFile(int id)
        {
            var fileDto = await _service.GetAttachmentAsync(id);

            if (fileDto == null || fileDto.Data == null)
                return NotFound("File content missing");

            return File(fileDto.Data, fileDto.ContentType, fileDto.FileName);
        }

        [HttpPost("PreviewMergeStamp")]
        public async Task<IActionResult> PreviewMergeStamp([FromForm] PreviewMergeStampRequestDto input, CancellationToken cancellationToken)
        {
            List<PreviewQuotationItemDto> quotationItems;
            try
            {
                quotationItems = JsonSerializer.Deserialize<List<PreviewQuotationItemDto>>(
                    input.QuotationsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<PreviewQuotationItemDto>();
            }
            catch (JsonException)
            {
                return BadRequest("รูปแบบ QuotationsJson ไม่ถูกต้อง");
            }

            if (quotationItems.Count == 0)
            {
                return BadRequest("กรุณาแนบไฟล์อย่างน้อย 1 ไฟล์");
            }

            var pendingFilesByName = (input.NewAttachments ?? new List<Microsoft.AspNetCore.Http.IFormFile>())
                .GroupBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => new Queue<Microsoft.AspNetCore.Http.IFormFile>(group));

            var pdfFiles = new List<PdfFileDto>();

            foreach (var item in quotationItems)
            {
                if (item.Id > 0)
                {
                    if (item.Id > int.MaxValue)
                    {
                        return BadRequest($"ค่า ID ไฟล์เดิมไม่ถูกต้อง: {item.Id}");
                    }

                    var existingFile = await _service.GetAttachmentAsync((int)item.Id);
                    if (existingFile?.Data == null)
                    {
                        return BadRequest($"ไม่พบไฟล์เอกสารสำหรับรายการ ID {item.Id}");
                    }

                    pdfFiles.Add(new PdfFileDto
                    {
                        Name = string.IsNullOrWhiteSpace(item.OriginalFileName) ? existingFile.FileName : item.OriginalFileName,
                        DocumentTypeId = item.DocumentTypeId <= 0 ? 10 : item.DocumentTypeId,
                        ContentType = existingFile.ContentType ?? "application/pdf",
                        Data = existingFile.Data,
                        Length = existingFile.Data.LongLength
                    });

                    continue;
                }

                var pendingName = string.IsNullOrWhiteSpace(item.OriginalFileName) ? item.FileName : item.OriginalFileName;
                if (string.IsNullOrWhiteSpace(pendingName) ||
                    !pendingFilesByName.TryGetValue(pendingName, out var pendingQueue) ||
                    pendingQueue.Count == 0)
                {
                    return BadRequest($"ไม่พบไฟล์ใหม่สำหรับรายการ '{pendingName}'");
                }

                var pendingFile = pendingQueue.Dequeue();
                bool isPdf = string.Equals(pendingFile.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                    || pendingFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

                if (!isPdf)
                {
                    return BadRequest($"ไฟล์ '{pendingFile.FileName}' ไม่ใช่ PDF");
                }

                using var memoryStream = new MemoryStream();
                await pendingFile.CopyToAsync(memoryStream, cancellationToken);

                pdfFiles.Add(new PdfFileDto
                {
                    Name = pendingFile.FileName,
                    DocumentTypeId = item.DocumentTypeId <= 0 ? 10 : item.DocumentTypeId,
                    ContentType = string.IsNullOrWhiteSpace(pendingFile.ContentType) ? "application/pdf" : pendingFile.ContentType,
                    Data = memoryStream.ToArray(),
                    Length = pendingFile.Length
                });
            }

            if (pdfFiles.Count == 0)
            {
                return BadRequest("ไม่พบข้อมูลไฟล์สำหรับ Preview");
            }

            var approvalSteps = new List<StepDto>();
            if (input.RequestId.HasValue && input.RequestId > 0)
            {
                var request = await _service.GetByIdAsync(input.RequestId.Value);
                if (request?.WorkflowRoute?.Steps != null && request.WorkflowRoute.Steps.Any())
                {
                    var mockApprovers = new[] { "John Smith (Engineering)", "Sarah Johnson (Manager)", "Michael Chen (Director)" };
                    approvalSteps = request.WorkflowRoute.Steps
                        .OrderBy(s => s.SequenceNo)
                        .Select((s, index) => new StepDto
                        {
                            StepName = s.StepName ?? $"Step {index + 1}",
                            Approver = mockApprovers[index % mockApprovers.Length],
                            ApprovalDate = DateTime.Now.AddDays(-(request.WorkflowRoute.Steps.Count - index))
                        }).ToList();
                }
            }

            if (approvalSteps.Count == 0)
            {
                approvalSteps = new List<StepDto>
                {
                    new StepDto
                    {
                        StepName = "Reviewed",
                        Approver = "John Smith (PREVIEW)",
                        ApprovalDate = DateTime.Now.AddDays(-3)
                    },
                    new StepDto
                    {
                        StepName = "Approved",
                        Approver = "Sarah Johnson (PREVIEW)",
                        ApprovalDate = DateTime.Now.AddDays(-2)
                    },
                    new StepDto
                    {
                        StepName = "Final Approval",
                        Approver = "Michael Chen (PREVIEW)",
                        ApprovalDate = DateTime.Now.AddDays(-1)
                    }
                };
            }

            var previewRequest = new MergeAndStampRequestDto
            {
                DocumentName = string.IsNullOrWhiteSpace(input.DocumentName) ? "Preview" : input.DocumentName,
                ReferenceCode = string.IsNullOrWhiteSpace(input.ReferenceCode) ? "PREVIEW" : input.ReferenceCode,
                PdfFiles = pdfFiles,
                ApprovalData = new ApprovalDataDto
                {
                    Name = "Preview Document",
                    Step = approvalSteps
                },
                DrawSetting = new DrawSettingDto
                {
                    Color = "#000000",
                    FontSize = 8,
                    Margin = 20,
                    AlignmentStamp = 8
                }
            };

            var previewFile = await _quotationService.GeneratePreviewMergedPdfAsync(previewRequest, "Preview", cancellationToken);
            if (previewFile.Data == null)
            {
                return StatusCode(500, "ไม่สามารถสร้างไฟล์ Preview ได้");
            }

            return File(previewFile.Data, previewFile.ContentType, previewFile.FileName);
        }

        [HttpGet("Rejected")]
        public object GetRejectedRequests(DataSourceLoadOptions loadOptions)
        {
            return LoadGrid(_service.GetRejectedRequestsQuery(), loadOptions);
        }

        private sealed class PreviewQuotationItemDto
        {
            public long Id { get; set; }
            public string? FileName { get; set; }
            public string? OriginalFileName { get; set; }
            public int DocumentTypeId { get; set; }
        }
    }
}