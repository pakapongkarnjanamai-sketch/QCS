using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;
using System.Text.Json;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseRequestController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public PurchaseRequestController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: api/PurchaseRequest/MyRequests
        [HttpGet("MyRequests")]
        public async Task<IActionResult> GetMyRequests()
        {
            // ในที่นี้เราจะดึงข้อมูลทั้งหมดก่อน (ในระบบจริงควร Filter ตาม User ID)
            var requests = await _context.PurchaseRequests
                .Include(r => r.Quotations)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // แปลงเป็น DTO ให้ตรงกับ MyRequestHistoryViewModel ฝั่งหน้าบ้าน
            var result = requests.Select(r => new
            {
                Id = r.Id,
                DocumentNo = r.DocumentNo,
                Title = r.Title,
                RequestDate = r.RequestDate,
                Status = r.Status,
                TotalAmount = r.Quotations.Where(q => q.IsSelected).Sum(q => q.TotalAmount), // ยอดรวมเฉพาะเจ้าที่เลือก
                CurrentHandler = GetCurrentHandler(r.Id), // Helper function
                IsCompleted = r.Status == "Completed"
            });

            return Ok(result);
        }

        // GET: api/PurchaseRequest/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRequestDetail(int id)
        {
            var request = await _context.PurchaseRequests
                .Include(r => r.Quotations)
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            // หา Step ปัจจุบันที่สถานะเป็น Pending
            var currentStep = request.ApprovalSteps
                .OrderBy(s => s.Sequence)
                .FirstOrDefault(s => s.Status == "Pending");

            var result = new
            {
                PurchaseRequestId = request.Id,
                DocumentNo = request.DocumentNo,
                Title = request.Title,
                RequestDate = request.RequestDate,
                Status = request.Status,
                RequesterName = request.CreatedBy ?? "System",
                CurrentStepId = currentStep?.Id ?? 0,
                Quotations = request.Quotations.Select(q => new
                {
                    Id = q.Id,
                    VendorName = q.VendorName,
                    TotalAmount = q.TotalAmount,
                    IsSelected = q.IsSelected,
                    OriginalFileName = q.OriginalFileName,
                    // สร้าง URL สำหรับ Download ไฟล์ (ต้องมี Action Download หรือเปิด Static File)
                    FilePath = q.FilePath // ในที่นี้ส่ง Path ไปก่อน
                })
            };

            return Ok(result);
        }

        // POST: api/PurchaseRequest/Create
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromForm] CreateRequestDto input)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. สร้าง Header
                var pr = new PurchaseRequest
                {
                    Title = input.Title,
                    RequestDate = input.RequestDate,
                    Status = "Pending",
                    DocumentNo = $"PR-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}", // Generate เลขที่เอกสาร
                    Quotations = new List<Quotation>(),
                    ApprovalSteps = new List<ApprovalStep>()
                };

                // 2. แปลง JSON Quotations
                var quotationItems = JsonSerializer.Deserialize<List<QuotationItemDto>>(input.QuotationsJson ?? "[]");

                // 3. จัดการไฟล์แนบ (Upload)
                string uploadFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "quotations");
                if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

                if (input.Attachments != null && quotationItems != null)
                {
                    foreach (var file in input.Attachments)
                    {
                        if (file.Length > 0)
                        {
                            // สร้างชื่อไฟล์ไม่ซ้ำ
                            string fileName = $"{Guid.NewGuid()}_{file.FileName}";
                            string filePath = Path.Combine(uploadFolder, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            // หา Item ใน JSON ที่ชื่อไฟล์ตรงกัน เพื่อเอาข้อมูล Vendor/Price
                            var info = quotationItems.FirstOrDefault(q => q.FileName == file.FileName);
                            if (info != null)
                            {
                                pr.Quotations.Add(new Quotation
                                {
                                    VendorName = info.VendorName,
                                    TotalAmount = info.TotalAmount,
                                    IsSelected = info.IsSelected,
                                    OriginalFileName = file.FileName,
                                    FilePath = $"/uploads/quotations/{fileName}" // Web Path
                                });
                            }
                        }
                    }
                }

                // 4. สร้าง Workflow การอนุมัติ (Mockup)
                // ในระบบจริงอาจจะดึงจาก Master Data ว่าต้องให้ใครเซ็นบ้าง
                pr.ApprovalSteps.Add(new ApprovalStep { Sequence = 1, ApproverName = "Manager A", Role = "Manager", Status = "Pending" });
                pr.ApprovalSteps.Add(new ApprovalStep { Sequence = 2, ApproverName = "Director B", Role = "Director", Status = "Pending" });

                _context.PurchaseRequests.Add(pr);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, id = pr.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private string GetCurrentHandler(int prId)
        {
            var step = _context.ApprovalSteps
                .Where(s => s.PurchaseRequestId == prId && s.Status == "Pending")
                .OrderBy(s => s.Sequence)
                .FirstOrDefault();
            return step?.ApproverName ?? "-";
        }
    }

  
}