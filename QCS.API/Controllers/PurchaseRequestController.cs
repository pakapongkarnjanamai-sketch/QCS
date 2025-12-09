using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Services;
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
        private readonly IWebHostEnvironment _env;
        private readonly WorkflowIntegrationService _workflowService;

        public PurchaseRequestController(
            AppDbContext context,
            IWebHostEnvironment env,
            WorkflowIntegrationService workflowService)
        {
            _context = context;
            _env = env;
            _workflowService = workflowService;
        }


        [HttpGet("Detail/{id}")]
        public async Task<IActionResult> GetRequestDetail(int id)
        {
            try
            {
                // 1. ดึงข้อมูลจาก DB (เหมือนเดิม)
                var request = await _context.PurchaseRequests
                    .Include(r => r.Quotations)
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request == null) return NotFound("ไม่พบข้อมูลเอกสาร");

                // 2. ดึงข้อมูล Workflow (เหมือนเดิม)
                var workflowRoute = await _workflowService.GetWorkflowRouteDetailAsync(1);

                // 3. คำนวณ Permission (เหมือนเดิม)
                bool canApprove = false;
                if (request.Status == StatusConsts.PR_Pending && workflowRoute != null)
                {
                    var currentStepConfig = workflowRoute.Steps
                        .FirstOrDefault(s => s.SequenceNo == (int)request.CurrentStep);

                    if (currentStepConfig != null)
                    {
                        if (currentStepConfig.Assignments == null || !currentStepConfig.Assignments.Any())
                        {
                            canApprove = true;
                        }
                        else
                        {
                            canApprove = currentStepConfig.Assignments.Any(a => a.IsCurrentUser);
                        }
                    }
                }

                // 4. Map DTO (แก้ไขเพิ่ม WorkflowRoute)
                var dto = new PurchaseRequestDetailDto
                {
                    PurchaseRequestId = request.Id,
                    DocumentNo = request.Code,
                    Title = request.Title,
                    RequestDate = request.RequestDate,
                    Status = request.Status.ToString(), // แก้เป็น int ตาม Enum ถ้าจำเป็น
                    VendorName = request.VendorName,
                    ValidFrom = request.ValidFrom,
                    ValidUntil = request.ValidUntil,
                    Comment = request.Comment,

                    Quotations = request.Quotations.Select(q => new QuotationDetailDto
                    {
                        Id = q.Id,
                        OriginalFileName = q.FileName,
                        FilePath = q.FilePath,
                        DocumentTypeId = q.DocumentTypeId
                    }).ToList(),

                    Permissions = new PurchaseRequestPermissionsDto
                    {
                        CanApprove = canApprove,
                        CanReject = canApprove
                    },

                    // === [NEW] ใส่ข้อมูล Workflow ลงไปตรงนี้ ===
                    WorkflowRoute = workflowRoute
                    // ======================================
                };

                return Ok(dto);
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
                var currentUserId = User.Identity?.Name;

                // สำหรับการทดสอบ (ถ้ายังไม่มี Auth จริง)
                // if (string.IsNullOrEmpty(currentUserId)) currentUserId = "h8197"; 

                var requests = await _context.PurchaseRequests
                    .Where(r => r.CreatedBy == currentUserId)
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
                    })
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        // ==========================================
        // 1. Endpoint สำหรับ "บันทึก (Save)" -> Draft
        // ==========================================
        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromForm] CreatePurchaseRequestDto input)
        {
            // isSubmit = false หมายถึงยังไม่ส่ง Workflow (เป็น Draft)
            return await ProcessCreation(input, isSubmit: false);
        }

        // ==========================================
        // 2. Endpoint สำหรับ "สร้าง (Create)" -> Submit
        // ==========================================
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromForm] CreatePurchaseRequestDto input)
        {
            // isSubmit = true หมายถึงส่ง Workflow ทันที
            return await ProcessCreation(input, isSubmit: true);
        }

        // ==========================================
        // Shared Logic Method
        // ==========================================
        private async Task<IActionResult> ProcessCreation(CreatePurchaseRequestDto input, bool isSubmit)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. ดึงข้อมูล Workflow Route (ID 1)
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);
                if (routeData == null || routeData.Steps == null)
                {
                    return BadRequest("ไม่สามารถดึงข้อมูล Workflow Route ได้");
                }

                var sortedSteps = routeData.Steps.OrderBy(s => s.SequenceNo).ToList();

                // 2. สร้าง Running Number (Format: PR-yyyyMMdd-XXX)
                var todayStr = DateTime.Now.ToString("yyyyMMdd");
                var prefix = $"PR-{todayStr}-";
                var countToday = await _context.PurchaseRequests
                    .Where(x => x.Code.StartsWith(prefix))
                    .CountAsync();

                var newDocNo = $"{prefix}{(countToday + 1):D3}";

                // 3. กำหนด CurrentStepId และ Status ของเอกสาร
                int currentStepId;
                int docStatus;

                if (isSubmit)
                {
                    // กรณี Create (ส่งอนุมัติ): 
                    // งานของ Step 1 (Purchaser) ถือว่าเสร็จแล้ว -> ส่งต่อไป Step 2
                    var nextStep = sortedSteps.FirstOrDefault(s => s.SequenceNo > 1);
                    if (nextStep != null)
                    {
                        currentStepId = nextStep.SequenceNo; // ไป Step 2
                        docStatus = StatusConsts.PR_Pending; // สถานะเอกสาร: รออนุมัติ
                    }
                    else
                    {
                        // ถ้า Workflow มีแค่ Step เดียว -> จบกระบวนการเลย
                        currentStepId = 99; // Completed sequence
                        docStatus = StatusConsts.PR_Completed;
                    }
                }
                else
                {
                    // กรณี Save (บันทึกร่าง):
                    // งานยังอยู่ที่ Step 1 (Purchaser)
                    currentStepId = 1;
                    docStatus = StatusConsts.PR_Draft; // สถานะเอกสาร: ร่าง
                }

                // 4. สร้าง Header (PurchaseRequest)
                var pr = new PurchaseRequest
                {
                    Code = newDocNo,
                    Title = input.Title,
                    RequestDate = DateTime.Now,

                    Status = docStatus,           // int
                    CurrentStepId = currentStepId, // int sequenceNo

                    VendorId = input.VendorId,
                    VendorName = input.VendorName,
                    ValidFrom = input.ValidFrom,
                    ValidUntil = input.ValidUntil,
                    Comment = input.Comment,

                    ApprovalSteps = new List<ApprovalStep>(),
                    Quotations = new List<Quotation>()
                };

                // 5. สร้าง Approval Steps (รายการอนุมัติย่อย)
                foreach (var step in sortedSteps)
                {
                    int stepStatus = StatusConsts.Step_Draft; // Default: ยังมาไม่ถึง
                    DateTime? actionDate = null;

                    if (step.SequenceNo == 1) // Step 1: Purchaser
                    {
                        if (isSubmit)
                        {
                            // ถ้ากด Create แสดงว่า Step 1 ทำเสร็จแล้ว -> Approved
                            stepStatus = StatusConsts.Step_Approved;
                            actionDate = DateTime.Now;
                        }
                        else
                        {
                            // ถ้ากด Save แสดงว่ากำลังทำอยู่ -> Pending
                            stepStatus = StatusConsts.Step_Pending;
                        }
                    }
                    else if (step.SequenceNo == 2) // Step 2: Verifier
                    {
                        if (isSubmit)
                        {
                            // ถ้าส่งงานมาแล้ว -> มารอที่ Step 2
                            stepStatus = StatusConsts.Step_Pending;
                        }
                        else
                        {
                            // ถ้ายังเป็น Draft -> ยังมาไม่ถึง
                            stepStatus = StatusConsts.Step_Draft;
                        }
                    }
                    // Step 3+ เป็น Draft/Waiting ทั้งหมด

                    var approvalStep = new ApprovalStep
                    {
                        Sequence = step.SequenceNo,
                        Role = step.StepName,
                        Status = stepStatus,
                        ActionDate = actionDate
                    };
                    pr.ApprovalSteps.Add(approvalStep);
                }

                // 6. จัดการไฟล์แนบ
                await HandleFileUploads(input, pr);

                // 7. บันทึกข้อมูล
                _context.PurchaseRequests.Add(pr);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, id = pr.Id, docNo = pr.Code, status = pr.Status });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

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