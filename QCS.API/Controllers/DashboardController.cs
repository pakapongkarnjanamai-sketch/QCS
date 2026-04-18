using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                // NOTE: These queries must run sequentially because they share the same DbContext.
                var myTaskCount = await _requestService.GetMyPendingTaskCountAsync();
                var myApprovedCount = await _requestService.GetMyApprovedListQuery().CountAsync();
                var myRejectedCount = await _requestService.GetRejectedRequestsQuery().CountAsync();

                return Ok(new DashboardDto
                {
                    MyTaskCount = myTaskCount,
                    MyApprovedCount = myApprovedCount,
                    MyRejectedCount = myRejectedCount,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}