using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Services;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;
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

        public DashboardController(AppDbContext context, WorkflowService workflowService)
        {
            _context = context;
            _workflowService = workflowService;
        }

        private string CurrentUserNId => User.Identity?.Name?.Split('\\').LastOrDefault() ?? "SYSTEM";

        [HttpGet("Summary")]
        public async Task<ActionResult<DashboardDto>> GetSummary()
        {
            try
            {
                var nId = CurrentUserNId;

                // 1. My Requests Stats (เอกสารที่ฉันสร้าง)
                // หมายเหตุ: ใช้ CreatedBy ถ้ามี หรือใช้ Logic อื่นตาม Database จริง
                var myRequestsQuery = _context.PurchaseRequests.AsQueryable();
                // .Where(r => r.CreatedBy == nId); // Uncomment เมื่อมี field CreatedBy

                var totalCreated = await myRequestsQuery.CountAsync();
                var totalPending = await myRequestsQuery.CountAsync(r => r.Status == (int)RequestStatus.Pending);
                var totalCompleted = await myRequestsQuery.CountAsync(r => r.Status == (int)RequestStatus.Approved || r.Status == (int)RequestStatus.Rejected);

                // 2. My Tasks Stats (งานที่รอฉันอนุมัติ)
                var myTaskCount = 0;

                // ดึง Workflow เพื่อดูว่า User อยู่ Step ไหน
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);
                if (routeData?.Steps != null)
                {
                    var myStepSequences = routeData.Steps
                        .Where(s => s.Assignments != null && s.Assignments.Any(a => a.NId == nId))
                        .Select(s => s.SequenceNo)
                        .ToList();

                    if (myStepSequences.Any())
                    {
                        myTaskCount = await _context.PurchaseRequests
                            .CountAsync(r => r.Status == (int)RequestStatus.Pending &&
                                           myStepSequences.Contains(r.CurrentStepId));
                    }
                }

                return Ok(new DashboardDto
                {
                    TotalCreated = totalCreated,
                    TotalPending = totalPending,
                    TotalCompleted = totalCompleted,
                    MyRequestCount = totalCreated, // Badge tab 1 (แสดงทั้งหมด หรือเฉพาะ Active ก็ได้)
                    MyTaskCount = myTaskCount      // Badge tab 2
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}