using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Services;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Infrastructure.Data;
using System.Security.Claims;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public DashboardController(AppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        [HttpGet("Summary")]
        public async Task<ActionResult<DashboardDto>> GetSummary()
        {
            try
            {
                var nId = _currentUserService.UserId;
                var myTaskCount = 0;

                // ปรับปรุง: นับจำนวนงานจาก DB ApprovalSteps โดยตรง แม่นยำกว่าการ Resolve Workflow สดๆ
                // เงื่อนไข: สถานะเอกสารเป็น Pending และ User เป็นผู้อนุมัติใน Step ปัจจุบัน
                myTaskCount = await _context.Requests.AsNoTracking()
                    .CountAsync(r => r.Status == (int)RequestStatus.Pending &&
                                     r.ApprovalSteps.Any(s => s.Sequence == r.CurrentStepId && s.ApproverNId == nId));

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