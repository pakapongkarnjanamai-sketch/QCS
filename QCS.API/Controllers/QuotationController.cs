using DevExtreme.AspNet.Data; // จำเป็นสำหรับ DataSourceLoader
using DevExtreme.AspNet.Mvc;  // จำเป็นสำหรับ DataSourceLoadOptions
using Microsoft.AspNetCore.Authorization;
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
        // ใช้ QuotationService สำหรับฟังก์ชันเฉพาะ เช่น การสร้าง PDF
        private readonly IQuotationService _quotationService;
        public QuotationController(
              IRequestService requestService,
              IQuotationService quotationService)
        {
            _requestService = requestService;
            _quotationService = quotationService;
        }

        // ==========================================================
        // 🔍 GET BY CODE (ย้ายมาจาก RequestController)
        // ==========================================================
        [HttpGet("ByCode")]
        public async Task<IActionResult> GetByCode(string code)
        {
            // เรียกใช้ได้เลย Service จะรู้เองว่าถ้า Approved แล้วต้องทำงานเร็วๆ
            var result = await _requestService.GetByCodeAsync(code);

            if (result == null) return NotFound("ไม่พบข้อมูลเอกสาร");

            return Ok(result);
        }

        [HttpGet("ViewFile/{id}")]
        public async Task<IActionResult> ViewFile(int id)
        {
            var fileDto = await _quotationService.GenerateStampedPdfAsync(id);

            if (fileDto == null || fileDto.Data == null)
                return NotFound("File content missing");

            return File(fileDto.Data, fileDto.ContentType, fileDto.FileName);
        }
    }
}