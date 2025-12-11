using Microsoft.AspNetCore.Authorization; // ถ้ามีการทำ Auth
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;
using System.Linq;
using System.Threading.Tasks;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize] // เปิดใช้งานเมื่อระบบ Login สมบูรณ์
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("my-summary")]
        public async Task<ActionResult<DashboardDto>> GetMyDashboard([FromQuery] int? userId)
        {
            // 1. Base Query: ดึงข้อมูล PR (ถ้ามี userId ให้ filter)
            var query = _context.Set<PurchaseRequest>().AsQueryable();

            if (userId.HasValue && userId > 0)
            {
                // query = query.Where(x => x.CreatedBy == userId); // ตัวอย่างถ้ามี field CreatedBy
            }

            // 2. คำนวณตัวเลขสรุป (ใช้ CountAsync เพื่อประสิทธิภาพ)
            var total = await query.CountAsync();
            var pending = await query.CountAsync(x => x.Status == (int)RequestStatus.Pending);
            var approved = await query.CountAsync(x => x.Status == (int)RequestStatus.Approved || x.Status == (int)RequestStatus.Completed);
            var rejected = await query.CountAsync(x => x.Status == (int)RequestStatus.Rejected);

            // 3. ดึงรายการล่าสุด 10 รายการ
            var recent = await query.OrderByDescending(x => x.Id) // หรือ CreatedDate
                                    .Take(10)
                                    .ToListAsync();

            // 4. ส่งกลับเป็น DTO
            var result = new DashboardDto
            {
                TotalRequests = total,
                PendingRequests = pending,
                ApprovedRequests = approved,
                RejectedRequests = rejected,
                RecentRequests = recent
            };

            return Ok(result);
        }
    }
}