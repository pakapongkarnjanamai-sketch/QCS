using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
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

        public PurchaseRequestController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/PurchaseRequest/MyRequests
        [HttpGet("MyRequests")]
        public async Task<IActionResult> GetMyRequests()
        {
            // ดึงข้อมูลทั้งหมด (ในระบบจริงควร Filter ตาม User ID)
            var requests = await _context.PurchaseRequests
                .Include(r => r.Quotations)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // แปลงเป็น DTO สำหรับแสดงผลรายการ
            var result = requests.Select(r => new
            {
                Id = r.Id,
                DocumentNo = r.DocumentNo,
                Title = r.Title,
                RequestDate = r.RequestDate,
                Status = r.Status,
                TotalAmount = r.Quotations.Where(q => q.IsSelected).Sum(q => q.TotalAmount), // ยอดรวมเฉพาะเจ้าที่เลือก
                CurrentHandler = GetCurrentHandler(r.Id),
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
                    .ThenInclude(q => q.AttachmentFile) // Include ข้อมูลไฟล์เพื่อเอา ID มาทำ Link
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
                    VendorId = q.VendorId,
                    VendorName = q.VendorName,
                    TotalAmount = q.TotalAmount,
                    IsSelected = q.IsSelected,

                    // ข้อมูลเพิ่มเติม
                    DocumentTypeId = q.DocumentTypeId,
                    ValidFrom = q.ValidFrom,
                    ValidUntil = q.ValidUntil,
                    Comment = q.Comment,

                    OriginalFileName = q.OriginalFileName,
                    // สร้าง URL สำหรับ Download ไฟล์จาก Action DownloadAttachment
                    FilePath = q.AttachmentFileId.HasValue
                        ? Url.Action("DownloadAttachment", new { id = q.AttachmentFileId })
                        : null
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
                // 1. สร้าง Running Number (Format: PR-yyyyMMdd-XXX)
                var todayStr = DateTime.Now.ToString("yyyyMMdd");
                var prefix = $"PR-{todayStr}-";

                // นับจำนวนใบ PR ที่สร้างในวันนี้เพื่อหาเลขถัดไป
                var countToday = await _context.PurchaseRequests
                    .Where(x => x.DocumentNo.StartsWith(prefix))
                    .CountAsync();

                var runningNo = (countToday + 1).ToString("D3"); // แปลงเป็น 001, 002, ...
                var newDocNo = $"{prefix}{runningNo}";

                // 2. สร้าง Header
                var pr = new PurchaseRequest
                {
                    Title = input.Title,
                    RequestDate = input.RequestDate,
                    Status = StatusConsts.PR_Pending, // ใช้ Constant
                    DocumentNo = newDocNo,
                    Quotations = new List<Quotation>(),
                    ApprovalSteps = new List<ApprovalStep>()
                };

                // ... (Logic การจัดการ Quotations และ Attachments คงเดิม) ...
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var quotationItems = JsonSerializer.Deserialize<List<QuotationItemDto>>(input.QuotationsJson ?? "[]", options);

                if (input.Attachments != null && quotationItems != null)
                {
                    foreach (var file in input.Attachments)
                    {
                        if (file.Length > 0)
                        {
                            using var memoryStream = new MemoryStream();
                            await file.CopyToAsync(memoryStream);

                            var attachment = new AttachmentFile
                            {
                                FileName = file.FileName,
                                ContentType = file.ContentType,
                                FileSize = file.Length,
                                Data = memoryStream.ToArray()
                            };

                            var info = quotationItems.FirstOrDefault(q => q.FileName == file.FileName);
                            if (info != null)
                            {
                                pr.Quotations.Add(new Quotation
                                {
                                    VendorName = info.VendorName,
                                    TotalAmount = info.TotalAmount,
                                    IsSelected = info.IsSelected,
                                    OriginalFileName = file.FileName,
                                    VendorId = info.VendorId,
                                    DocumentTypeId = info.DocumentTypeId,
                                    ValidFrom = info.ValidFrom,
                                    ValidUntil = info.ValidUntil,
                                    Comment = info.Comment,
                                    AttachmentFile = attachment
                                });
                            }
                        }
                    }
                }

                // 3. สร้าง Workflow (ตัวอย่าง)
                pr.ApprovalSteps.Add(new ApprovalStep { Sequence = 1, ApproverName = "Manager A", Role = "Manager", Status = StatusConsts.Step_Pending });
                pr.ApprovalSteps.Add(new ApprovalStep { Sequence = 2, ApproverName = "Director B", Role = "Director", Status = StatusConsts.Step_Pending });

                _context.PurchaseRequests.Add(pr);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, id = pr.Id, docNo = newDocNo });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Action สำหรับ Download ไฟล์จาก Database
        [HttpGet("DownloadAttachment/{id}")]
        public async Task<IActionResult> DownloadAttachment(int id)
        {
            var file = await _context.AttachmentFiles.FindAsync(id);
            if (file == null) return NotFound();

            // ส่งไฟล์กลับไปให้ Browser ดาวน์โหลด
            return File(file.Data, file.ContentType ?? "application/octet-stream", file.FileName);
        }

        // Helper function
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