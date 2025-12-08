using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs;
using QCS.Domain.Enum; // หรือ StatusConsts
using QCS.Domain.Models;
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

        // POST: api/Approval/Approve
        [HttpPost("Approve")]
        public async Task<IActionResult> Approve([FromBody] ApprovalActionDto input)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. ดึงข้อมูล PR และ Step ที่เกี่ยวข้อง
                var request = await _context.PurchaseRequests
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == input.PurchaseRequestId);

                if (request == null) return NotFound("Purchase Request not found.");

                // หา Step ปัจจุบันที่รออนุมัติ (เรียงตาม Sequence)
                var currentStep = request.ApprovalSteps
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault(s => s.Status == StatusConsts.Step_Pending);

                if (currentStep == null)
                    return BadRequest("No pending approval step found or document is already processed.");

                // TODO: ในระบบจริง ต้องเช็คว่า User ปัจจุบันมีสิทธิ์อนุมัติ Step นี้หรือไม่ (เช็คจาก Role/User ID)
                // if (currentStep.ApproverId != currentUserId) return Unauthorized();

                // 2. อัปเดตสถานะ Step นี้
                currentStep.Status = StatusConsts.Step_Approved;
         
                currentStep.Comment = input.Comment; // ความเห็นเพิ่มเติม (ถ้ามี)
                // currentStep.ApproverId = ... (บันทึกคนกดจริง)

                // 3. ตรวจสอบว่าเป็น Step สุดท้ายหรือไม่?
                var nextStep = request.ApprovalSteps
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault(s => s.Sequence > currentStep.Sequence);

                if (nextStep == null)
                {
                    // ถ้าไม่มี Step ต่อไปแล้ว -> จบกระบวนการ (PR Approved)
                    request.Status = StatusConsts.PR_Approved;
                }
                else
                {
                    // ถ้ามี Step ต่อไป -> สถานะ PR ยังเป็น Pending (รอคนต่อไป)
                    // อาจจะมีการส่ง Noti ให้คนถัดไปตรงนี้
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Approved successfully", newStatus = request.Status });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        // POST: api/Approval/Reject
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

                var currentStep = request.ApprovalSteps
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefault(s => s.Status == StatusConsts.Step_Pending);

                if (currentStep == null)
                    return BadRequest("No pending approval step found.");

                // 1. อัปเดต Step เป็น Rejected
                currentStep.Status = StatusConsts.Step_Rejected;

                currentStep.Comment = input.Comment; // เหตุผลที่ไม่อนุมัติ

                // 2. อัปเดต Header เป็น Rejected ทันที (จบข่าว)
                request.Status = StatusConsts.PR_Rejected;

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