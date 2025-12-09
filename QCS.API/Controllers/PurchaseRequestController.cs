using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using QCS.Application.Services;
using QCS.Domain.DTOs;
using QCS.Domain.Enum; // ใช้ Enum/Const เพื่อความชัดเจน
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
        private readonly WorkflowIntegrationService _workflowService; // เพิ่ม Service นี้

        public PurchaseRequestController(
            AppDbContext context,
            IWebHostEnvironment env,
            WorkflowIntegrationService workflowService)
        {
            _context = context;
            _env = env;
            _workflowService = workflowService;
        }

        // GET: api/PurchaseRequest/Detail/{id}
        [HttpGet("Detail/{id}")]
        public async Task<IActionResult> GetRequestDetail(int id)
        {
            try
            {
                var request = await _context.PurchaseRequests
                    .Include(r => r.Quotations)
                    .Include(r => r.ApprovalSteps) // โหลด Step ที่สร้างไว้
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request == null) return NotFound("ไม่พบข้อมูลเอกสาร");

                return Ok(request);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }
        // GET: api/PurchaseRequest/MyRequests
        [HttpGet("MyRequests")]
        public async Task<IActionResult> GetMyRequests()
        {
            try
            {
                // 1. ระบุตัวตน User ปัจจุบัน
                // สมมติว่า User.Identity.Name เก็บ User ID หรือ Username ที่ใช้ใน field CreatedBy
                // หรือถ้าใช้ JWT Claims ให้ดึงจาก ClaimTypes.NameIdentifier
                var currentUserId = User.Identity?.Name;

                if (string.IsNullOrEmpty(currentUserId))
                {
                    // กรณี Test Local อาจจะยังไม่มี Auth ให้ Return ทั้งหมดไปก่อน หรือ Return 401
                    // return Unauthorized("User not authenticated."); 

                    // *สำหรับการทดสอบช่วงแรก ถ้ายังไม่ Login:*
                    // สามารถ comment 2 บรรทัดบน แล้ว hardcode ค่าไปก่อนได้ เช่น:
                    // currentUserId = "h8197"; 
                }

                // 2. Query ข้อมูลเฉพาะของ User นั้น
                var requests = await _context.PurchaseRequests
                    .Where(r => r.CreatedBy == currentUserId) // กรองด้วย CreatedBy
                    .OrderByDescending(r => r.RequestDate)
                    .Select(r => new
                    {
                        Id = r.Id,
                        Code = r.Code,
                        Title = r.Title,
                        RequestDate = r.RequestDate,
                        Status = r.Status,
                        VendorName = r.VendorName,
                        CurrentStepId = r.CurrentStepId
                        // Map fields อื่นๆ ตามต้องการ
                    })
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
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
                // 1. ดึงข้อมูล Workflow Route (ID 1) เพื่อมาสร้าง Step ใน Database
                // (ถ้าอนาคตมีหลาย Route ต้องรับ RouteId มาจาก input)
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);

                if (routeData == null || routeData.Steps == null)
                {
                    return BadRequest("ไม่สามารถดึงข้อมูล Workflow Route ได้");
                }

                // 2. สร้าง Running Number (Format: PR-yyyyMMdd-XXX)
                var todayStr = DateTime.Now.ToString("yyyyMMdd");
                var prefix = $"PR-{todayStr}-";
                var countToday = await _context.PurchaseRequests
                    .Where(x => x.Code.StartsWith(prefix))
                    .CountAsync();

                var newDocNo = $"{prefix}{(countToday + 1):D3}";

                // 3. สร้าง Header (PurchaseRequest)
                var pr = new PurchaseRequest
                {
                    Code = newDocNo,
                    Title = input.Title,
                    RequestDate = DateTime.Now,

                    // สถานะเริ่มต้น
                    Status = StatusConsts.PR_Pending,

                    // ข้อมูลจาก Form
                    VendorId = input.VendorId,
                    VendorName = input.VendorName,
                    ValidFrom = input.ValidFrom,
                    ValidUntil = input.ValidUntil,
                    Comment = input.Comment,

                    // Initialize Collections
                    ApprovalSteps = new List<ApprovalStep>(),
                    Quotations = new List<Quotation>()
                };

                // 4. *** สำคัญ *** สร้าง ApprovalSteps จาก Workflow API ลง Database
                // เพื่อให้ ApprovalController ทำงานต่อได้
                var sortedSteps = routeData.Steps.OrderBy(s => s.SequenceNo).ToList();

                foreach (var step in sortedSteps)
                {
                    var isFirstStep = step.SequenceNo == sortedSteps.First().SequenceNo;

                    var approvalStep = new ApprovalStep
                    {
                        Sequence = step.SequenceNo, // 1, 2, 3...
                        Role = step.StepName,       // Purchaser, Manager...
             
                        // Step แรกสถานะเป็น Pending (รออนุมัติ) ส่วน Step อื่นๆ รอ (Draft/Waiting)
                        Status = isFirstStep ? StatusConsts.Step_Pending : StatusConsts.Step_Draft,

                        // บันทึกวันที่เริ่มต้นเฉพาะ Step แรก
                        ActionDate = null
                    };

                    // (Optional) ถ้าต้องการบันทึกชื่อผู้อนุมัติลงไปเลย (snapshot) ทำตรงนี้ได้
                    // string assigneeNames = string.Join(",", step.Assignments.Select(a => a.EmployeeName));
                    // approvalStep.ApproverName = assigneeNames;

                    pr.ApprovalSteps.Add(approvalStep);
                }

                // 5. กำหนด Current Step ของ Header ให้ตรงกับ Step แรก
                if (sortedSteps.Any())
                {
                    var firstSeq = sortedSteps.First().SequenceNo;
                    // แปลง Sequence เป็น Enum (ถ้าตรงกัน) หรือเก็บเป็น Int
                    if (Enum.IsDefined(typeof(WorkflowStep), firstSeq))
                    {
                        pr.CurrentStep = (WorkflowStep)firstSeq;
                    }
                    else
                    {
                        pr.CurrentStepId = firstSeq;
                    }
                }

                // 6. จัดการไฟล์แนบ
                await HandleFileUploads(input, pr);

                // 7. บันทึกข้อมูล
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

        // แยก Logic Upload File ออกมาเพื่อให้อ่านง่าย
        private async Task HandleFileUploads(CreatePurchaseRequestDto input, PurchaseRequest pr)
        {
            if (input.Attachments == null || input.Attachments.Count == 0) return;

            var uploadPath = Path.Combine(_env.WebRootPath, "uploads", DateTime.Now.ToString("yyyyMM"));
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            var metaDataList = JsonSerializer.Deserialize<List<QuotationItemDto>>(
                input.QuotationsJson ?? "[]",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            foreach (var file in input.Attachments)
            {
                if (file.Length > 0)
                {
                    var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                    var fullPath = Path.Combine(uploadPath, uniqueFileName);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var meta = metaDataList?.FirstOrDefault(m => m.FileName == file.FileName)
                               ?? new QuotationItemDto { DocumentTypeId = 10 };

                    pr.Quotations.Add(new Quotation
                    {
                        FileName = file.FileName,
                        FilePath = Path.Combine("uploads", DateTime.Now.ToString("yyyyMM"), uniqueFileName),
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        DocumentTypeId = meta.DocumentTypeId
                    });
                }
            }
        }
    }
}