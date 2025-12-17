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

                // =========================================================================================
                // 1. My Task Stats (งานที่รอฉันอนุมัติ)
                // =========================================================================================
                var myTaskCount = 0;

                // ดึง Workflow เพื่อดูว่า User อยู่ Step ไหน
                // (สามารถ Cache routeData ได้ถ้า Workflow ไม่เปลี่ยนบ่อย เพื่อความเร็วสูงสุด)
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);

                var myStepSequences = new List<int>();
                if (routeData?.Steps != null)
                {
                    myStepSequences = routeData.Steps
                        .Where(s => s.Assignments != null && s.Assignments.Any(a => a.NId == nId))
                        .Select(s => s.SequenceNo)
                        .ToList();
                }

                // Query หลัก: ใช้ AsNoTracking() เพื่อความเร็ว (Read-Only)
                var requestsQuery = _context.Requests.AsNoTracking();

                if (myStepSequences.Any())
                {
                    myTaskCount = await requestsQuery
                        .CountAsync(r => r.Status == (int)RequestStatus.Pending &&
                                         myStepSequences.Contains(r.CurrentStepId));
                }

                // =========================================================================================
                // 2. My Requests Stats (เอกสารที่ฉันสร้าง) - Optimized with GroupBy
                // =========================================================================================
                // ดึง Count แยกตาม Status ใน Query เดียว (แทนที่จะ Select Count 3-4 รอบ)
                var statusCounts = await requestsQuery
                    .Where(r => r.CreatedBy == nId) // กรองเฉพาะของฉัน
                    .GroupBy(r => r.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status, x => x.Count);

                // ดึงค่าจาก Dictionary (ถ้าไม่มีค่าให้เป็น 0)
                int GetCount(RequestStatus status) => statusCounts.TryGetValue((int)status, out int val) ? val : 0;

                var pendingCount = GetCount(RequestStatus.Pending);
                var approvedCount = GetCount(RequestStatus.Approved);
                var rejectedCount = GetCount(RequestStatus.Rejected);
                var draftCount = GetCount(RequestStatus.Draft);

                // คำนวณผลรวมตาม Business Logic
                // TotalCreated: ปกติมักหมายถึง "เอกสารที่ดำเนินการอยู่" (Pending + Draft + Rejected หรือทั้งหมดที่ไม่ใช่ Approved)
                // หรือถ้าต้องการนับ "ทั้งหมดที่เคยสร้าง" ก็เอาทุกค่ามาบวกกัน
                var totalActive = pendingCount + draftCount + rejectedCount;

                return Ok(new DashboardDto
                {
                    // TotalCreated = จำนวนที่ยังไม่เสร็จสมบูรณ์ (Active)
                    TotalCreated = totalActive,

                    // TotalPending = รออนุมัติ
                    TotalPending = pendingCount,

                    // TotalCompleted = อนุมัติแล้ว
                    TotalCompleted = approvedCount,

                    // MyRequestCount = Active Requests ของฉัน
                    MyRequestCount = totalActive,

                    // MyTaskCount = งานที่รอฉันอนุมัติ
                    MyTaskCount = myTaskCount,

                    // ApprovedCount = แยกส่งไปให้ Frontend เผื่อใช้
                    ApprovedCount = approvedCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}