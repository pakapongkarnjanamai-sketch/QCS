using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        // POST: api/Approval/Action
        [HttpPost("Action")]
        public async Task<IActionResult> TakeAction([FromBody] ApprovalActionDto input)
        {
            var step = await _context.ApprovalSteps
                .Include(s => s.PurchaseRequest) // Load PR มาด้วยเพื่อแก้สถานะ
                .FirstOrDefaultAsync(s => s.Id == input.StepId);

            if (step == null) return NotFound("Step not found");
            if (step.Status != "Pending") return BadRequest("This step is already processed.");

            // 1. อัปเดตสถานะของ Step นี้
            step.Status = input.IsApproved ? "Approved" : "Rejected";
            step.Comment = input.Comment;
            step.ApprovalDate = DateTime.Now;

            // 2. อัปเดตสถานะของเอกสารหลัก (PurchaseRequest)
            var pr = step.PurchaseRequest; // ใช้ Navigation Property (ต้องมั่นใจว่า Model มี public PurchaseRequest PurchaseRequest { get; set; })

            // ถ้าไม่มี Navigation Property ให้ Query เอา:
            if (pr == null) pr = await _context.PurchaseRequests.FindAsync(step.PurchaseRequestId);

            if (!input.IsApproved)
            {
                // ถ้า Reject -> จบข่าว เอกสาร Reject ทันที
                pr.Status = "Rejected";
            }
            else
            {
                // ถ้า Approve -> เช็คว่ามี Step ต่อไปไหม
                var nextStep = await _context.ApprovalSteps
                    .Where(s => s.PurchaseRequestId == pr.Id && s.Sequence > step.Sequence)
                    .OrderBy(s => s.Sequence)
                    .FirstOrDefaultAsync();

                if (nextStep == null)
                {
                    // ไม่มี Step ต่อไปแล้ว -> อนุมัติเสร็จสิ้นสมบูรณ์
                    pr.Status = "Completed";

                    // TODO: เรียก PDF API (Merge & Stamp) ตรงจุดนี้
                }
                else
                {
                    // ยังมี Step ต่อไป -> เอกสารยัง Pending อยู่ (รอคนถัดไป)
                    pr.Status = $"Pending {nextStep.Role}";
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }

    public class ApprovalActionDto
    {
        public int StepId { get; set; }
        public string Comment { get; set; }
        public bool IsApproved { get; set; }
    }
}