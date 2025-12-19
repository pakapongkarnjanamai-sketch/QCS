using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Services;
using QCS.Domain.DTOs;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService _service;

        public RequestController(IRequestService service)
        {
            _service = service;
        }

        // ==========================================================
        // ⚡ DATA GRID ENDPOINTS
        // ==========================================================

        [HttpGet("GetMyRequests")]
        public object GetMyRequests(DataSourceLoadOptions loadOptions)
        {
            var query = _service.GetMyRequestsQuery();
            return DataSourceLoader.Load(query, loadOptions);
        }

        [HttpGet("GetMyTasks")]
        public async Task<object> GetMyTasks(DataSourceLoadOptions loadOptions)
        {
            var query = await _service.GetMyTasksQueryAsync();
            return DataSourceLoader.Load(query, loadOptions);
        }

        [HttpGet("ApprovedList")]
        public object GetApprovedList(DataSourceLoadOptions loadOptions)
        {
            var query = _service.GetApprovedListQuery();
            return DataSourceLoader.Load(query, loadOptions);
        }
        [HttpGet("MyApprovedList")]
        public object GetMyApprovedList(DataSourceLoadOptions loadOptions)
        {
            var query = _service.GetMyApprovedListQuery();
            return DataSourceLoader.Load(query, loadOptions);
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

        [HttpGet("DetailByCode/{code}")]
        public async Task<IActionResult> GetRequestDetailByCode(string code)
        {
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

        [HttpPost("SubmitCreate")] // Optional: Endpoint for "Save & Submit"
        public async Task<IActionResult> SubmitCreate([FromForm] CreateRequestDto input)
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

        [HttpGet("GetRejectedRequests")]
        public object GetRejectedRequests(DataSourceLoadOptions loadOptions)
        {
            var query = _service.GetRejectedRequestsQuery();
            return DataSourceLoader.Load(query, loadOptions);
        }
    }
}