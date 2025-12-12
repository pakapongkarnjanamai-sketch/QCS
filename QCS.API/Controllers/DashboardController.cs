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

                var Query = _context.PurchaseRequests.AsQueryable();
                var myRequestsQuery = Query.Where(r => r.Status != (int)RequestStatus.Approved);

                // .Where(r => r.CreatedBy == nId); // Uncomment เมื่อมี field CreatedBy

                var totalCreated = await myRequestsQuery.CountAsync();

                // รออนุมัติ
                var totalPending = await Query.CountAsync(r => r.Status == (int)RequestStatus.Pending);

                // [Modified] อนุมัติ/เสร็จสิ้น (ไม่นับ Rejected ตาม Requirement)
                // นับสถานะ Approved (2) และ Completed (3)
                var totalApproved = await Query.CountAsync(r => r.Status == (int)RequestStatus.Approved || r.Status == (int)RequestStatus.Completed);

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
                    TotalCompleted = totalApproved, // ใช้ค่าที่กรอง Rejected ออกแล้ว
                    MyRequestCount = totalCreated,
                    MyTaskCount = myTaskCount,
                    ApprovedCount = totalApproved   // [New] ส่งค่าไปสำหรับ Tab ใหม่
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}