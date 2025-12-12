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
        private readonly IQuotationService _quotationService;


        public QuotationController(
            IQuotationService quotationService
           )
        {
            _quotationService = quotationService;
          
        }

        // ==========================================================
        // 🔍 GET BY CODE (ย้ายมาจาก RequestController)
        // ==========================================================
        [HttpGet("ByCode")]
        public object GetByCode(string code, DataSourceLoadOptions loadOptions)
        {
            // ใช้ Service ของ Request ดึงข้อมูล PR ตาม Code (รวม Quotation และ ApprovalSteps แล้ว)
            var source = _quotationService.GetQueryable().Where(x => x.Code == code);

            // ส่งกลับให้ DevExtreme Grid
            return DataSourceLoader.Load(source, loadOptions);
        }

        // ==========================================================
        // 📥 VIEW FILE
        // ==========================================================
        [HttpGet("ViewFile/{id}")]
        public async Task<IActionResult> ViewFile(int id)
        {
            var fileDto = await _quotationService.GetAttachmentAsync(id);

            if (fileDto == null || fileDto.Data == null)
                return NotFound("File content missing");

            return File(fileDto.Data, fileDto.ContentType, fileDto.FileName);
        }
    }
}