using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Services;
using QCS.Domain.DTOs;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService _service;

        // ✅ Inject Service เข้ามาแทน DbContext และ WorkflowService
        public RequestController(IRequestService service)
        {
            _service = service;
        }

        // ==========================================================
        // 🔍 GET DETAIL
        // ==========================================================
        [HttpGet("Detail/{id}")]
        public async Task<IActionResult> GetRequestDetail(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound("ไม่พบข้อมูลเอกสาร");
            return Ok(result);
        }

        // ==========================================================
        // 📋 LISTS (Specialized Queries)
        // ==========================================================
        [HttpGet("MyRequests")]
        public async Task<IActionResult> GetMyRequests()
        {
            var result = await _service.GetMyRequestsAsync();
            return Ok(result);
        }

        [HttpGet("PendingApprovals")]
        public async Task<IActionResult> GetPendingApprovals()
        {
            var result = await _service.GetPendingApprovalsAsync();
            return Ok(result);
        }

        [HttpGet("ApprovedList")]
        public async Task<IActionResult> GetApprovedList()
        {
            var result = await _service.GetApprovedListAsync();
            return Ok(result);
        }

        // ==========================================================
        // 💾 ACTIONS (Create / Update / Submit)
        // ==========================================================

        [HttpPost("Save")] // บันทึกเป็น Draft
        public async Task<IActionResult> Save([FromForm] CreatePurchaseRequestDto input)
        {
            // ส่ง flag isSubmit = false ไปให้ Service
            var result = await _service.CreateAsync(input, isSubmit: false);
            return Ok(new { success = true, id = result.Id, docNo = result.Code });
        }

        [HttpPost("Submit")] // บันทึกและส่งอนุมัติทันที
        public async Task<IActionResult> Submit([FromForm] CreatePurchaseRequestDto input)
        {
            // ส่ง flag isSubmit = true ไปให้ Service
            var result = await _service.CreateAsync(input, isSubmit: true);
            return Ok(new { success = true, id = result.Id, docNo = result.Code });
        }

        [HttpPost("Update")] // แก้ไข Draft
        public async Task<IActionResult> Update([FromForm] UpdatePurchaseRequestDto input)
        {
            await _service.UpdateAsync(input, isSubmit: false);
            return Ok(new { success = true });
        }

        [HttpPost("SubmitUpdate")] // แก้ไขและส่งอนุมัติใหม่
        public async Task<IActionResult> SubmitUpdate([FromForm] UpdatePurchaseRequestDto input)
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

        // ==========================================================
        // 📥 FILE HANDLING
        // ==========================================================
        [HttpGet("ViewFile/{id}")]
        public async Task<IActionResult> ViewFile(int id)
        {
            // ให้ Service ไปหาไฟล์มา (ไม่ว่าจะจาก DB หรือ Disk) แล้วคืนเป็น Model กลาง
            var fileDto = await _service.GetAttachmentAsync(id);

            if (fileDto == null || fileDto.Data == null)
                return NotFound("File content missing");

            // Controller มีหน้าที่แค่ Return FileResult
            return File(fileDto.Data, fileDto.ContentType, fileDto.FileName);
        }
    }
}