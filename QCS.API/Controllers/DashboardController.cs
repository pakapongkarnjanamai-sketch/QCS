using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Services;
using QCS.Domain.DTOs;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        // ✅ เปลี่ยน Type เป็น Interface
        private readonly IRequestService _requestService;

        // ✅ เปลี่ยน Parameter ใน Constructor เป็น Interface
        public DashboardController(IRequestService requestService)
        {
            _requestService = requestService;
        }

        [HttpGet("Summary")]
        public async Task<ActionResult<DashboardDto>> GetSummary()
        {
            try
            {
                var myTaskCount = await _requestService.GetMyPendingTaskCountAsync();

                return Ok(new DashboardDto
                {
                    MyTaskCount = myTaskCount,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}