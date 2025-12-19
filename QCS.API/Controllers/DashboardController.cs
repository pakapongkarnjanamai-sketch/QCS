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
        private readonly WorkflowService _workflowService;
        private readonly ICurrentUserService _currentUserService;
        public DashboardController(AppDbContext context, WorkflowService workflowService, ICurrentUserService currentUserService)
        {
            _context = context;
            _workflowService = workflowService;
            _currentUserService = currentUserService;
        }

        [HttpGet("Summary")]
        public async Task<ActionResult<DashboardDto>> GetSummary()
        {
            try
            {
                var nId = _currentUserService.UserId;
                var myTaskCount = 0;

                // 1. ดึง Workflow เพื่อดูว่า User อยู่ Step ไหน (เน้นเฉพาะที่เกี่ยวข้องกับ Task)
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);

                if (routeData?.Steps != null)
                {
                    var myStepSequences = routeData.Steps
                        .Where(s => s.Assignments != null && s.Assignments.Any(a => a.NId == nId))
                        .Select(s => s.SequenceNo)
                        .ToList();

                    if (myStepSequences.Any())
                    {
                        // Query เฉพาะจำนวนงานที่ต้องทำ
                        myTaskCount = await _context.Requests.AsNoTracking()
                            .CountAsync(r => r.Status == (int)RequestStatus.Pending &&
                                             myStepSequences.Contains(r.CurrentStepId));
                    }
                }

                // ส่งกลับเฉพาะ MyTaskCount ส่วนค่าอื่นให้เป็น 0 เพื่อไม่ให้กระทบ DTO เดิม
                return Ok(new DashboardDto
                {
                    MyTaskCount = myTaskCount,
                    MyRequestCount = 0,
                    ApprovedCount = 0,
                    TotalCreated = 0,
                    TotalPending = 0,
                    TotalCompleted = 0
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}