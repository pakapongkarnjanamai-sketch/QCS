using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Services;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Infrastructure.Data;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ApprovalController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly WorkflowService _workflowService;
        private readonly ICurrentUserService _currentUserService; // ✅ เพิ่ม Field

        public ApprovalController(
            AppDbContext context,
            WorkflowService workflowService,
            ICurrentUserService currentUserService) // ✅ Inject เข้ามา
        {
            _context = context;
            _workflowService = workflowService;
            _currentUserService = currentUserService;
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
                // 🔐 SECURITY CHECK: ตรวจสอบสิทธิ์กับ Workflow API
                // ==========================================================
                int routeId = 1; // ควรย้ายไป Config ในอนาคต
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(routeId);

                // หา Configuration ของ Step นี้
                var stepConfig = routeData?.Steps?.FirstOrDefault(s => s.SequenceNo == currentStepObj.Sequence);

                // ✅ ใช้ User ID จาก Service กลาง
                string currentUserId = _currentUserService.UserId;

                bool isAuthorized = false;
                if (stepConfig != null && stepConfig.Assignments != null)
                {
                    // เช็คว่า User ปัจจุบันมีอยู่ในรายการ Assignments ของ Step นี้ไหม
                    isAuthorized = stepConfig.Assignments.Any(a => a.NId.Equals(currentUserId, StringComparison.OrdinalIgnoreCase));
                }

                if (!isAuthorized)
                {
                    // (Optional) อาจมี Logic Super User / Admin ให้ผ่านได้เสมอ
                    return StatusCode(403, new { message = $"คุณไม่มีสิทธิ์อนุมัติในขั้นตอนนี้ (Step: {stepConfig?.StepName})" });
                }
                // ==========================================================

                // 🌐 FETCH NAME: ดึงชื่อจริงจาก Workflow มาบันทึก
                string approverName = await _workflowService.GetEmployeeNameFromWorkflowAsync(routeId, currentUserId);
                if (string.IsNullOrEmpty(approverName)) approverName = currentUserId;

                // ✅ ACTION: บันทึกว่า "ใคร" เป็นคนทำจริงๆ
                currentStepObj.Status = (int)RequestStatus.Approved;
                currentStepObj.ActionDate = DateTime.Now;
                currentStepObj.Comment = input.Comment;
                currentStepObj.ApproverNId = currentUserId; // <-- บันทึก ID ที่ได้จาก Service
                currentStepObj.ApproverName = approverName;

                // ... [Logic การหา Next Step] ...
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

                // ✅ ใช้ User ID จาก Service กลาง
                string currentUserId = _currentUserService.UserId;

                bool isAuthorized = false;
                if (stepConfig != null && stepConfig.Assignments != null)
                {
                    isAuthorized = stepConfig.Assignments.Any(a => a.NId.Equals(currentUserId, StringComparison.OrdinalIgnoreCase));
                }

                if (!isAuthorized)
                {
                    return StatusCode(403, new { message = "คุณไม่มีสิทธิ์ปฏิเสธในขั้นตอนนี้" });
                }

                // ดึงชื่อ
                string approverName = await _workflowService.GetEmployeeNameFromWorkflowAsync(routeId, currentUserId);
                if (string.IsNullOrEmpty(approverName)) approverName = currentUserId;

                // ✅ RECORD REJECTION
                currentStepObj.Status = (int)RequestStatus.Rejected;
                currentStepObj.ActionDate = DateTime.Now;
                currentStepObj.Comment = input.Comment;
                currentStepObj.ApproverNId = currentUserId; // <-- บันทึก ID
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