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
    public class ApprovalController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly WorkflowService _workflowService;

        public ApprovalController(AppDbContext context, WorkflowService workflowService)
        {
            _context = context;
            _workflowService = workflowService;
        }

        // Helper: ดึง User ID (nId) ของคนที่ Login อยู่
        private string CurrentUserNId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                         ?? User.FindFirst("nId")?.Value
                                         ?? "SYSTEM";

        [HttpPost("Approve")]
        public async Task<IActionResult> Approve([FromBody] ApprovalActionDto input)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. ดึงข้อมูล PR และ Steps
                var request = await _context.PurchaseRequests
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == input.PurchaseRequestId);

                if (request == null) return NotFound("ไม่พบเอกสาร Purchase Request");

                // 2. หา Step ปัจจุบัน (ต้องเป็น Pending เท่านั้น สำหรับการอนุมัติ)
                var currentStepObj = request.ApprovalSteps
                    .Where(s => s.Status == (int)RequestStatus.Pending)
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault();

                if (currentStepObj == null)
                    return BadRequest("ไม่พบขั้นตอนที่รอการอนุมัติในขณะนี้");

                // ==========================================================
                // 🔐 SECURITY CHECK: ตรวจสอบสิทธิ์คนกด
                // ==========================================================
                if (!string.IsNullOrEmpty(currentStepObj.ApproverNId) &&
                    !currentStepObj.ApproverNId.Equals(CurrentUserNId, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { message = $"คุณไม่มีสิทธิ์อนุมัติในขั้นตอนนี้ (Required: {currentStepObj.ApproverNId})" });
                }

                // ==========================================================
                // 🌐 FETCH NAME: ดึงชื่อจาก Workflow API
                // ==========================================================
                // สมมติ RouteID = 1 (หรือดึงจาก request ถ้ามี field เก็บไว้)
                int routeId = 1;
                string approverName = await _workflowService.GetEmployeeNameFromWorkflowAsync(routeId, CurrentUserNId);

                // Fallback: ถ้า API หาไม่เจอ ให้ใช้ NId หรือชื่อจาก DB (ถ้ามี) หรือค่าเดิม
                if (string.IsNullOrEmpty(approverName))
                {
                    approverName = currentStepObj.ApproverName ?? CurrentUserNId;
                }

                // 3. อัปเดต Step ปัจจุบัน -> Approved
                currentStepObj.Status = (int)RequestStatus.Approved;
                currentStepObj.ActionDate = DateTime.Now;
                currentStepObj.Comment = input.Comment;
                currentStepObj.ApproverNId = CurrentUserNId; // บันทึกคนกดจริง
                currentStepObj.ApproverName = approverName;  // บันทึกชื่อที่ได้จาก API

                // 4. หา Step ถัดไป
                var nextStepObj = request.ApprovalSteps
                    .Where(s => s.Sequence > currentStepObj.Sequence)
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault();

                if (nextStepObj == null)
                {
                    // === จบกระบวนการ (Completed) ===
                    request.Status = (int)RequestStatus.Approved;
                    request.CurrentStep = WorkflowStep.Completed;
                }
                else
                {
                    // === มี Step ถัดไป ===
                    // ปลุก Step ถัดไป (Draft -> Pending)
                    if (nextStepObj.Status == (int)RequestStatus.Draft)
                    {
                        nextStepObj.Status = (int)RequestStatus.Pending;
                    }

                    // อัปเดต Header
                    request.CurrentStepId = nextStepObj.Sequence;
                    if (Enum.IsDefined(typeof(WorkflowStep), nextStepObj.Sequence))
                    {
                        request.CurrentStep = (WorkflowStep)nextStepObj.Sequence;
                    }
                    request.Status = (int)RequestStatus.Pending;
                }

                _context.Update(request);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Approved successfully", newStatus = request.Status });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        [HttpPost("Reject")]
        public async Task<IActionResult> Reject([FromBody] ApprovalActionDto input)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await _context.PurchaseRequests
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == input.PurchaseRequestId);

                if (request == null) return NotFound("ไม่พบเอกสาร Purchase Request");

                // 2. หา Step ที่จะ Reject (Pending หรือ Draft)
                var currentStepObj = request.ApprovalSteps
                    .Where(s => s.Status == (int)RequestStatus.Pending || s.Status == (int)RequestStatus.Draft)
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault();

                if (currentStepObj == null)
                    return BadRequest("ไม่พบขั้นตอนที่สามารถปฏิเสธได้");

                // 🔐 SECURITY CHECK
                if (!string.IsNullOrEmpty(currentStepObj.ApproverNId) &&
                    !currentStepObj.ApproverNId.Equals(CurrentUserNId, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { message = "คุณไม่มีสิทธิ์ปฏิเสธในขั้นตอนนี้" });
                }

                // 🌐 FETCH NAME: ดึงชื่อจาก Workflow API
                int routeId = 1;
                string approverName = await _workflowService.GetEmployeeNameFromWorkflowAsync(routeId, CurrentUserNId);
                if (string.IsNullOrEmpty(approverName)) approverName = currentStepObj.ApproverName ?? CurrentUserNId;

                // 3. อัปเดต Step นี้ -> Rejected
                currentStepObj.Status = (int)RequestStatus.Rejected;
                currentStepObj.ActionDate = DateTime.Now;
                currentStepObj.Comment = input.Comment;
                currentStepObj.ApproverNId = CurrentUserNId;
                currentStepObj.ApproverName = approverName;

                // 4. อัปเดต Header -> Rejected (จบกระบวนการทันที)
                request.Status = (int)RequestStatus.Rejected;
                request.CurrentStep = WorkflowStep.Rejected;

                // ==========================================================
                // 🧹 CLEANUP: Reset ขั้นตอนที่เหลือเป็น Cancelled
                // ==========================================================
                var remainingSteps = request.ApprovalSteps
                    .Where(s => s.Sequence > currentStepObj.Sequence)
                    .ToList();

                foreach (var step in remainingSteps)
                {
                    // ต้องมั่นใจว่าใน Enum RequestStatus มีค่า Cancelled (เช่น 8)
                    // ถ้าไม่มี ให้ใช้ Draft หรือ Rejected ตามตกลง
                    step.Status = (int)RequestStatus.Cancelled;
                }
                // ==========================================================

                _context.Update(request);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Rejected successfully", newStatus = request.Status });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}