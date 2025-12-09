using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Infrastructure.Data;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApprovalController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ApprovalController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("Approve")]
        public async Task<IActionResult> Approve([FromBody] ApprovalActionDto input)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. ดึงข้อมูล PR พร้อม Steps
                var request = await _context.PurchaseRequests
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == input.PurchaseRequestId);

                if (request == null) return NotFound("Purchase Request not found.");

                // 2. หา Step ปัจจุบันที่กำลังรออนุมัติ (ใช้ CurrentStep หรือหาจาก Status ก็ได้)
                // เพื่อความชัวร์ หาจาก ApprovalSteps ที่สถานะ Pending และ Sequence ต่ำสุด
                var currentStepObj = request.ApprovalSteps
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault(s => s.Status == StatusConsts.Step_Pending);

                if (currentStepObj == null)
                    return BadRequest("เอกสารนี้ไม่มีขั้นตอนที่รออนุมัติ หรือสิ้นสุดกระบวนการแล้ว");

                // --- (Optional) ตรวจสอบสิทธิ์ผู้ใช้งานตรงนี้ ---
                // var currentUserId = User.Identity.Name...;
                // if (currentStepObj.ApproverNId != currentUserId) return Unauthorized();
                // ---------------------------------------------

                // 3. อัปเดตสถานะของ Step นี้
                currentStepObj.Status = StatusConsts.Step_Approved; // 2
                currentStepObj.ActionDate = DateTime.Now;
                currentStepObj.Comment = input.Comment;

                // 4. หา Step ถัดไป และอัปเดต CurrentStep ของ PR
                var nextStepObj = request.ApprovalSteps
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault(s => s.Sequence > currentStepObj.Sequence);

                if (nextStepObj == null)
                {
                    // === กรณี: ไม่มี Step ต่อไปแล้ว (จบ Process) ===
                    request.Status = StatusConsts.PR_Approved; // 2
                    request.CurrentStep = WorkflowStep.Completed; // Enum 99
                }
                else
                {
                    // === กรณี: มี Step ต่อไป ===
                    // อัปเดต CurrentStepId ให้ชี้ไปที่ Step ถัดไป (เพื่อให้ FE หรือ Query อื่นๆ รู้ว่าตอนนี้งานอยู่ที่ใคร)

                    // หมายเหตุ: การ Map Sequence ไปหา Enum อาจต้องดูว่า Sequence ใน Database 
                    // ตรงกับค่า Int ของ Enum หรือไม่ ถ้าตรงกันใช้ได้เลย
                    // สมมติ: Seq 1 = Purchaser(1), Seq 2 = Verifier(2), Seq 3 = Manager(3)

                    if (Enum.IsDefined(typeof(WorkflowStep), nextStepObj.Sequence))
                    {
                        request.CurrentStep = (WorkflowStep)nextStepObj.Sequence;
                    }
                    else
                    {
                        // Fallback ถ้า Sequence ไม่ตรงกับ Enum เป๊ะๆ (เช่น Seq 10, 20)
                        // อาจจะต้องเขียน Logic Map หรือเก็บค่า Sequence ไว้ใน Enum
                        // ในที่นี้ขอสมมติว่า Sequence ตรงกับ Enum ID
                        request.CurrentStepId = nextStepObj.Sequence;
                    }

                    // สถานะ PR ยังคงเป็น Pending (1)
                    request.Status = StatusConsts.PR_Pending;
                }

                _context.Update(request);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Approved successfully", newStatus = request.Status, currentStep = request.CurrentStep });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error: {ex.Message}");
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

                if (request == null) return NotFound("Purchase Request not found.");

                var currentStepObj = request.ApprovalSteps
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault(s => s.Status == StatusConsts.Step_Pending);

                if (currentStepObj == null)
                    return BadRequest("No pending approval step found.");

                // 1. อัปเดต Step เป็น Rejected
                currentStepObj.Status = StatusConsts.Step_Rejected; // 9
                currentStepObj.ActionDate = DateTime.Now;
                currentStepObj.Comment = input.Comment;

                // 2. อัปเดต Header PR
                request.Status = StatusConsts.PR_Rejected; // 9
                request.CurrentStep = WorkflowStep.Rejected; // Enum -1

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