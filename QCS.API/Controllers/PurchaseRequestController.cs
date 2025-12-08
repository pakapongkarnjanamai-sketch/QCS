using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs; // อย่าลืมสร้าง DTOs ตามที่คุยกันไว้
using QCS.Domain.Enum; // หรือ StatusConsts
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
        private readonly IWebHostEnvironment _env; // เพิ่มเพื่อจัดการไฟล์บน Server

        public PurchaseRequestController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: api/PurchaseRequest/MyRequests
        [HttpGet("MyRequests")]
        public async Task<IActionResult> GetMyRequests()
        {
            var requests = await _context.PurchaseRequests
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            // Mapping DTO สำหรับรายการ (ปรับตามความเหมาะสม)
            var result = requests.Select(r => new
            {
                Id = r.Id,
                DocumentNo = r.Code, // Map จาก Code
                Title = r.Title,
                RequestDate = r.RequestDate,
                Status = GetStatusString(r.Status),
                VendorName = r.VendorName,
                TotalAmount = 0 // ถ้ามี Field Amount ให้ใส่ตรงนี้
            });

            return Ok(result);
        }

        // GET: api/PurchaseRequest/{id}
        [HttpGet("Detail/{id}")]
        public async Task<IActionResult> GetRequestDetail(int id)
        {
            try
            {
                var request = await _context.PurchaseRequests
                    // 1. ดึงข้อมูล Items (รายการสินค้า)
                    // .Include(r => r.Items) 

                    // 2. ดึงข้อมูล Quotations (เอกสารแนบ) -> สำคัญสำหรับการแสดงผลในตาราง
                    .Include(r => r.Quotations)

                    // 3. ดึงข้อมูล Approval Steps (ประวัติการอนุมัติ)
                    .Include(r => r.ApprovalSteps)

                    // ❌ REMOVED: .Include(r => r.CreatedBy) 
                    // เอาออกเพราะ CreatedBy เป็น string (UserId) ไม่ใช่ Object Navigation

                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request == null)
                {
                    return NotFound("ไม่พบข้อมูลเอกสาร");
                }

                // Optional: ถ้าคุณต้องการส่ง RequesterName ไปหน้าบ้านจริงๆ
                // คุณอาจต้อง query user เพิ่มเติมที่นี่ (ถ้าใน DB เก็บแค่ ID)
                // var user = _context.Users.Find(request.CreatedBy);
                // request.RequesterName = user?.FullName; 

                return Ok(request);
            }
            catch (Exception ex)
            {
                // Log error เพื่อการตรวจสอบ
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        // POST: api/PurchaseRequest/Create
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromForm] CreatePurchaseRequestDto input)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. สร้าง Running Number (Format: PR-yyyyMMdd-XXX)
                var todayStr = DateTime.Now.ToString("yyyyMMdd");
                var prefix = $"PR-{todayStr}-";
                var countToday = await _context.PurchaseRequests
                    .Where(x => x.Code.StartsWith(prefix))
                    .CountAsync();

                var newDocNo = $"{prefix}{(countToday + 1):D3}";

                // 2. สร้าง Header (PurchaseRequest)
                var pr = new PurchaseRequest
                {
                    Code = newDocNo,
                    Title = input.Title,
                    RequestDate = DateTime.Now,
                    Status = 1, // 1 = Pending

                    // รับค่า Header จาก Form
                    VendorId = input.VendorId,
                    VendorName = input.VendorName,
                    ValidFrom = input.ValidFrom,
                    ValidUntil = input.ValidUntil,
                    Comment = input.Comment,

                    // TODO: ใส่ User ID ผู้สร้างจาก Token (User.Identity.Name หรือ Claim)
                    // CreatedById = ... 
                };

                // 3. จัดการไฟล์แนบ (Save to Disk)
                // path: wwwroot/uploads/YYYYMM/
                var uploadPath = Path.Combine(_env.WebRootPath, "uploads", DateTime.Now.ToString("yyyyMM"));
                if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                if (input.Attachments != null && input.Attachments.Count > 0)
                {
                    // แปลง JSON metadata กลับเป็น Object (ต้องเรียงลำดับให้ตรงกับไฟล์ที่ส่งมา)
                    var metaDataList = JsonSerializer.Deserialize<List<QuotationItemDto>>(
                        input.QuotationsJson ?? "[]",
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    for (int i = 0; i < input.Attachments.Count; i++)
                    {
                        var file = input.Attachments[i];
                        if (file.Length > 0)
                        {
                            // Gen ชื่อไฟล์ใหม่กันซ้ำ
                            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                            var fullPath = Path.Combine(uploadPath, uniqueFileName);

                            // บันทึกไฟล์ลง Disk
                            using (var stream = new FileStream(fullPath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            // หา Metadata ที่คู่กัน (ถ้าใช้ index หรือ match ด้วยชื่อไฟล์)
                            // ในที่นี้สมมติว่า Frontend ส่งมาลำดับตรงกัน หรือ match ด้วยชื่อ
                            var meta = metaDataList?.FirstOrDefault(m => m.FileName == file.FileName)
                                       ?? new QuotationItemDto { DocumentTypeId = 10 }; // Default

                            pr.Quotations.Add(new Quotation
                            {
                                FileName = file.FileName,
                                FilePath = Path.Combine("uploads", DateTime.Now.ToString("yyyyMM"), uniqueFileName), // เก็บ Relative Path
                                ContentType = file.ContentType,
                                FileSize = file.Length,
                                DocumentTypeId = meta.DocumentTypeId
                            });
                        }
                    }
                }

                _context.PurchaseRequests.Add(pr);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, id = pr.Id, docNo = pr.Code });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/PurchaseRequest/DownloadAttachment/5
        [HttpGet("DownloadAttachment/{id}")]
        public async Task<IActionResult> DownloadAttachment(int id)
        {
            var quotation = await _context.Quotations.FindAsync(id);
            if (quotation == null) return NotFound("File record not found.");

            // สร้าง Full Path จาก wwwroot
            var filePath = Path.Combine(_env.WebRootPath, quotation.FilePath);

            if (!System.IO.File.Exists(filePath))
                return NotFound("Physical file not found on server.");

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, quotation.ContentType ?? "application/octet-stream", quotation.FileName);
        }

        // Helper Method
        private string GetStatusString(int status)
        {
            return status switch
            {
                0 => "Draft",
                1 => "Pending",
                2 => "Approved",
                9 => "Rejected",
                _ => "Unknown"
            };
        }
    }
}