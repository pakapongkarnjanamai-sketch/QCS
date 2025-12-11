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


        // ==========================================================
        // 🔑 HELPER: CURRENT USER (Logic เดียวกับ WorkflowService)
        // ==========================================================
        private string CurrentUserNId
        {
            get
            {
                var fullIdentityName = User.Identity?.Name; // เช่น "DOMAIN\n4734"
                if (string.IsNullOrEmpty(fullIdentityName)) return "SYSTEM";

                var parts = fullIdentityName.Split('\\');
                // เอาส่วนข้างหลัง \ ถ้ามี หรือเอาทั้งหมดถ้าไม่มี
                return parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        [HttpPost("Approve")]
        public async Task<IActionResult> Approve([FromBody] ApprovalActionDto input)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await _context.PurchaseRequests
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == input.PurchaseRequestId);

                if (request == null) return NotFound("ไม่พบเอกสาร Purchase Request");

                // หา Step ปัจจุบัน (ต้องเป็น Pending)
                var currentStepObj = request.ApprovalSteps
                    .Where(s => s.Status == (int)RequestStatus.Pending)
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault();

                if (currentStepObj == null)
                    return BadRequest("ไม่พบขั้นตอนที่รอการอนุมัติในขณะนี้");

                // ==========================================================
                // 🔐 SECURITY CHECK: ตรวจสอบสิทธิ์กับ Workflow API (Plan)
                // ==========================================================
                // เนื่องจากใน DB เราปล่อยว่างไว้ (null) เราจึงต้องไปดูว่า "ใครควรจะเป็นคนอนุมัติ" จาก Workflow

                int routeId = 1; // สมมติ Route ID
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(routeId);

                // หา Configuration ของ Step นี้
                var stepConfig = routeData?.Steps?.FirstOrDefault(s => s.SequenceNo == currentStepObj.Sequence);

                bool isAuthorized = false;
                if (stepConfig != null && stepConfig.Assignments != null)
                {
                    // เช็คว่า User ปัจจุบันมีอยู่ในรายการ Assignments ของ Step นี้ไหม
                    isAuthorized = stepConfig.Assignments.Any(a => a.NId.Equals(CurrentUserNId, StringComparison.OrdinalIgnoreCase));
                }

                if (!isAuthorized)
                {
                    // (Optional) อาจมี Logic Super User / Admin ให้ผ่านได้เสมอ
                    return StatusCode(403, new { message = $"คุณไม่มีสิทธิ์อนุมัติในขั้นตอนนี้ (Step: {stepConfig?.StepName})" });
                }
                // ==========================================================

                // 🌐 FETCH NAME: ดึงชื่อจริงจาก Workflow มาบันทึก
                string approverName = await _workflowService.GetEmployeeNameFromWorkflowAsync(routeId, CurrentUserNId);
                if (string.IsNullOrEmpty(approverName)) approverName = CurrentUserNId;

                // ✅ ACTION: บันทึกว่า "ใคร" เป็นคนทำจริงๆ
                currentStepObj.Status = (int)RequestStatus.Approved;
                currentStepObj.ActionDate = DateTime.Now;
                currentStepObj.Comment = input.Comment;
                currentStepObj.ApproverNId = CurrentUserNId; // <-- บันทึกตอนกระทำ
                currentStepObj.ApproverName = approverName;  // <-- บันทึกตอนกระทำ

                // ... [Logic การหา Next Step เหมือนเดิม] ...
                var nextStepObj = request.ApprovalSteps
                    .Where(s => s.Sequence > currentStepObj.Sequence)
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault();

                if (nextStepObj == null)
                {
                    request.Status = (int)RequestStatus.Approved;
                    request.CurrentStep = WorkflowStep.Completed;
                }
                else
                {
                    // ปลุก Step ถัดไป (Draft -> Pending)
                    // ข้อสังเกต: Step ถัดไป ใน DB ก็ยังเป็น null อยู่ ซึ่งถูกต้องแล้ว รอให้คนถัดไปมากด
                    if (nextStepObj.Status == (int)RequestStatus.Draft)
                    {
                        nextStepObj.Status = (int)RequestStatus.Pending;
                    }

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

                if (request == null) return NotFound("ไม่พบเอกสาร");

                var currentStepObj = request.ApprovalSteps
                    .Where(s => s.Status == (int)RequestStatus.Pending || s.Status == (int)RequestStatus.Draft)
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault();

                if (currentStepObj == null) return BadRequest("ไม่พบขั้นตอน");

                // 🔐 SECURITY CHECK (Logic เดียวกับ Approve)
                int routeId = 1;
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(routeId);
                var stepConfig = routeData?.Steps?.FirstOrDefault(s => s.SequenceNo == currentStepObj.Sequence);

                bool isAuthorized = false;
                if (stepConfig != null && stepConfig.Assignments != null)
                {
                    isAuthorized = stepConfig.Assignments.Any(a => a.NId.Equals(CurrentUserNId, StringComparison.OrdinalIgnoreCase));
                }

                if (!isAuthorized)
                {
                    return StatusCode(403, new { message = "คุณไม่มีสิทธิ์ปฏิเสธในขั้นตอนนี้" });
                }

                // ดึงชื่อ
                string approverName = await _workflowService.GetEmployeeNameFromWorkflowAsync(routeId, CurrentUserNId);
                if (string.IsNullOrEmpty(approverName)) approverName = CurrentUserNId;

                // ✅ RECORD REJECTION
                currentStepObj.Status = (int)RequestStatus.Rejected;
                currentStepObj.ActionDate = DateTime.Now;
                currentStepObj.Comment = input.Comment;
                currentStepObj.ApproverNId = CurrentUserNId; // <-- บันทึกตอนกระทำ
                currentStepObj.ApproverName = approverName;

                // Update Header
                request.Status = (int)RequestStatus.Rejected;
                request.CurrentStep = WorkflowStep.Rejected;

                // Clean up remaining
                var remainingSteps = request.ApprovalSteps.Where(s => s.Sequence > currentStepObj.Sequence);
                foreach (var step in remainingSteps) step.Status = (int)RequestStatus.Cancelled;

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