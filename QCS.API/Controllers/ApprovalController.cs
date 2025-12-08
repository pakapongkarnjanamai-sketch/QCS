using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Infrastructure.Data;
using System.Security.Claims; // สำหรับดึง User ปัจจุบัน

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

        [HttpPost("Action")]
        public async Task<IActionResult> TakeAction([FromBody] ApprovalActionDto input)
        {
            // 1. ดึง Step และ Include PR มาด้วย
            var step = await _context.ApprovalSteps
                .Include(s => s.PurchaseRequest)
                .FirstOrDefaultAsync(s => s.Id == input.StepId);

            if (step == null) return NotFound("Step not found");

            // 2. Validate สถานะ: ต้องเป็น Pending เท่านั้นถึงจะทำรายการได้
            if (step.Status != StatusConsts.Step_Pending)
                return BadRequest("This step is already processed.");

            // 3. Security Check: เช็คว่า User ที่ Login คือคนที่ต้องอนุมัติหรือไม่?
            // (สมมติว่าใน Token เรามี Claim ชื่อ "Name" หรือ "Role")
            // var currentUserName = User.FindFirst(ClaimTypes.Name)?.Value;
            // var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // if (step.ApproverName != currentUserName && step.Role != currentUserRole)
            // {
            //      return Unauthorized("You are not authorized to approve this document.");
            // }

            // 4. อัปเดตสถานะของ Step
            step.Status = input.IsApproved ? StatusConsts.Step_Approved : StatusConsts.Step_Rejected;
            step.Comment = input.Comment;
            step.ApprovalDate = DateTime.Now;

            // 5. อัปเดตสถานะของ PR หลัก
            var pr = step.PurchaseRequest;

            if (!input.IsApproved)
            {
                // กรณีไม่อนุมัติ -> เอกสารตกทันที
                pr.Status = StatusConsts.PR_Rejected;
            }
            else
            {
                // กรณีอนุมัติ -> เช็คว่ามี Step ถัดไปไหม
                var nextStep = await _context.ApprovalSteps
                    .Where(s => s.PurchaseRequestId == pr.Id && s.Sequence > step.Sequence)
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefaultAsync();

                if (nextStep == null)
                {
                    // ไม่มี Step ต่อไป -> จบกระบวนการ
                    pr.Status = StatusConsts.PR_Completed;

                    // TODO: Trigger Email หรือ สร้าง PDF Final ที่นี่
                }
                else
                {
                    // มี Step ต่อไป -> รอคนถัดไป
                    pr.Status = $"{StatusConsts.PR_Pending} {nextStep.Role}";
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}