using Microsoft.AspNetCore.Mvc;
using PDF.Service.Interface;
using PDF.Service.Models;

namespace PDF.Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PdfController : ControllerBase
    {
        private readonly IPdfGeneratorService _pdfGeneratorService;

        public PdfController(IPdfGeneratorService pdfGeneratorService)
        {
            _pdfGeneratorService = pdfGeneratorService;
        }

        [HttpPost("merge-stamp")]
        public IActionResult MergeAndStamp([FromBody] MergeAndStampRequest request)
        {
            if (request?.PdfFiles is not { Count: > 0 })
                return BadRequest("กรุณาส่งไฟล์ PDF อย่างน้อย 1 ไฟล์");

            try
            {
                var stampedFiles = request.PdfFiles
                    .OrderBy(file => file.DocumentTypeId)
                    .Select(file => _pdfGeneratorService.Stamp(file, request.ApprovalData, request.DrawSetting, request.ReferenceCode))
                    .ToList();

                var docName = string.IsNullOrWhiteSpace(request.DocumentName)
                    ? "Merged-Document"
                    : request.DocumentName;
                var finalDoc = _pdfGeneratorService.Merge(stampedFiles, docName);

                return File(finalDoc.Data, "application/pdf", $"{finalDoc.Name}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}