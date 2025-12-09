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
                // 1. ดึงข้อมูล Request พร้อม ApprovalSteps
                var request = await _context.PurchaseRequests
                    .Include(r => r.Quotations)
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request == null) return NotFound("ไม่พบข้อมูลเอกสาร");

                // 2. ดึง Workflow Route
                var workflowRoute = await _workflowService.GetWorkflowRouteDetailAsync(1);

                // 3. === [NEW] Merge ประวัติการอนุมัติลงใน Workflow Route ===
                if (workflowRoute != null && workflowRoute.Steps != null)
                {
                    // ดึงรายชื่อ User ที่เกี่ยวข้องทั้งหมดเพื่อมา map ชื่อ (จาก UpdatedBy)
                    var approverIds = request.ApprovalSteps
                        .Where(x => !string.IsNullOrEmpty(x.UpdatedBy))
                        .Select(x => x.UpdatedBy)
                        .Distinct()
                        .ToList();

                    var usersMap = await _context.Users
                        .Where(u => approverIds.Contains(u.EmployeeID) || approverIds.Contains(u.NID)) // เช็คตาม field ที่เก็บใน UpdatedBy
                        .ToDictionaryAsync(u => u.EmployeeID, u => $"{u.FirstName} {u.LastName}"); // หรือใช้ Key ที่เหมาะสม

                    foreach (var step in workflowRoute.Steps)
                    {
                        // หา Step ที่ตรงกันใน DB
                        var history = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == step.SequenceNo);
                        if (history != null)
                        {
                            step.Status = history.Status;
                            step.ActionDate = history.ActionDate;
                            step.Comment = history.Comment;

                            // พยายามหาชื่อคนอนุมัติ
                            if (!string.IsNullOrEmpty(history.UpdatedBy) && usersMap.ContainsKey(history.UpdatedBy))
                            {
                                step.ApproverName = usersMap[history.UpdatedBy];
                            }
                            else
                            {
                                step.ApproverName = history.UpdatedBy; // fallback แสดง ID ไปก่อน
                            }
                        }
                    }
                }

                // 4. คำนวณ Permission (เหมือนเดิม)
                bool canApprove = false;
                bool canEdit = false;
                var currentUserId = User.Identity?.Name;
                if (request.Status == StatusConsts.PR_Draft && request.CreatedBy == currentUserId)
                {
                    canEdit = true;
                }
                if (request.Status == StatusConsts.PR_Pending && workflowRoute != null)
                {
                    var currentStepConfig = workflowRoute.Steps
                        .FirstOrDefault(s => s.SequenceNo == (int)request.CurrentStep); // หรือ request.CurrentStepId

                    if (currentStepConfig != null)
                    {
                        if (currentStepConfig.Assignments == null || !currentStepConfig.Assignments.Any())
                            canApprove = true;
                        else
                            canApprove = currentStepConfig.Assignments.Any(a => a.IsCurrentUser);
                    }
                }

                // 5. Map DTO ส่งกลับ
                var dto = new PurchaseRequestDetailDto
                {
                    PurchaseRequestId = request.Id,
                    DocumentNo = request.Code,
                    Title = request.Title,
                    RequestDate = request.RequestDate,
                    Status = request.Status.ToString(),
                    VendorName = request.VendorName,
                    ValidFrom = request.ValidFrom,
                    ValidUntil = request.ValidUntil,
                    Remark = request.Remark,
                    CurrentStepId = (int)request.CurrentStep, // ส่ง CurrentStepId ไปด้วยเพื่อใช้ Highlight Grid

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
                        CanReject = canApprove,
                        CanEdit = canEdit
                    },

                    WorkflowRoute = workflowRoute // ข้อมูลนี้มี Status/Remark ติดไปด้วยแล้ว
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
        [HttpPost("Submit")] // <--- เปลี่ยนชื่อจาก Create เป็น Submit
        public async Task<IActionResult> Submit([FromForm] CreatePurchaseRequestDto input)
        {
            // isSubmit = true -> Status จะเป็น Pending, Step 1 = Approved, ส่งไป Step 2
            return await ProcessCreation(input, isSubmit: true);
        }
        // ==========================================
        // 3. Endpoint สำหรับ "บันทึกแก้ไข (Update)" -> สถานะยังคงเป็น Draft
        // ==========================================
        [HttpPost("Update")]
        public async Task<IActionResult> Update([FromForm] UpdatePurchaseRequestDto input)
        {
            return await ProcessUpdate(input, isSubmit: false);
        }

        // ==========================================
        // 4. Endpoint สำหรับ "บันทึกและส่งอนุมัติ (Submit Update)" -> เปลี่ยนสถานะเป็น Pending
        // ==========================================
        [HttpPost("SubmitUpdate")]
        public async Task<IActionResult> SubmitUpdate([FromForm] UpdatePurchaseRequestDto input)
        {
            return await ProcessUpdate(input, isSubmit: true);
        }

        // ==========================================
        // Shared Update Logic
        // ==========================================
        private async Task<IActionResult> ProcessUpdate(UpdatePurchaseRequestDto input, bool isSubmit)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. ดึงข้อมูล
                var pr = await _context.PurchaseRequests
                    .Include(r => r.Quotations)
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == input.Id);

                if (pr == null) return NotFound("ไม่พบข้อมูลเอกสาร");

                // ... (ตรวจสอบสิทธิ์) ...

                // 2. อัปเดตข้อมูลทั่วไป (เหมือนเดิม)
                pr.Title = input.Title;
                pr.VendorId = input.VendorId;
                pr.VendorName = input.VendorName;
                pr.ValidFrom = input.ValidFrom;
                pr.ValidUntil = input.ValidUntil;
                pr.Remark = input.Remark;

                // 3. === [NEW] อัปเดตประเภทเอกสารของไฟล์เดิม ===
                if (!string.IsNullOrEmpty(input.UpdatedQuotationsJson))
                {
                    var updates = JsonSerializer.Deserialize<List<UpdateQuotationItemDto>>(
                        input.UpdatedQuotationsJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (updates != null)
                    {
                        foreach (var update in updates)
                        {
                            var existingFile = pr.Quotations.FirstOrDefault(q => q.Id == update.Id);
                            if (existingFile != null)
                            {
                                existingFile.DocumentTypeId = update.DocumentTypeId;
                            }
                        }
                    }
                }

                // 4. ลบไฟล์ (เหมือนเดิม)
                if (!string.IsNullOrEmpty(input.DeletedFileIds))
                {
                    var idsToDelete = input.DeletedFileIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                          .Select(int.Parse).ToList();
                    var filesToRemove = pr.Quotations.Where(q => idsToDelete.Contains(q.Id)).ToList();
                    foreach (var file in filesToRemove)
                    {
                        // System.IO.File.Delete(...) // ลบไฟล์จริงถ้าต้องการ
                        _context.Quotations.Remove(file);
                    }
                }

                // 5. === [UPDATED] เพิ่มไฟล์ใหม่ พร้อมระบุ Type ===
                if (input.NewAttachments != null && input.NewAttachments.Count > 0)
                {
                    var uploadPath = Path.Combine(_env.WebRootPath, "uploads", DateTime.Now.ToString("yyyyMM"));
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    // Deserilaize ข้อมูล Metadata ของไฟล์ใหม่
                    var newFilesMeta = JsonSerializer.Deserialize<List<QuotationItemDto>>(
                        input.QuotationsJson ?? "[]",
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    foreach (var file in input.NewAttachments)
                    {
                        if (file.Length > 0)
                        {
                            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                            var fullPath = Path.Combine(uploadPath, uniqueFileName);

                            using (var stream = new FileStream(fullPath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            // หา Type จากชื่อไฟล์
                            var meta = newFilesMeta?.FirstOrDefault(m => m.FileName == file.FileName)
                                       ?? new QuotationItemDto { DocumentTypeId = 10 }; // Default

                            pr.Quotations.Add(new Quotation
                            {
                                FileName = file.FileName,
                                FilePath = Path.Combine("uploads", DateTime.Now.ToString("yyyyMM"), uniqueFileName),
                                ContentType = file.ContentType,
                                FileSize = file.Length,
                                DocumentTypeId = meta.DocumentTypeId // <--- ใช้ค่าที่ส่งมา
                            });
                        }
                    }
                }

                // 6. Workflow Logic (เหมือนเดิม)
                if (isSubmit)
                {
                    pr.Status = StatusConsts.PR_Pending;
                    var step1 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 1);
                    if (step1 != null) { step1.Status = StatusConsts.Step_Approved; step1.ActionDate = DateTime.Now; }
                    var step2 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 2);
                    if (step2 != null) { step2.Status = StatusConsts.Step_Pending; pr.CurrentStepId = 2; }
                }

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

                // 2. สร้าง Running Number (Format: QC-yyyyMMdd-XXX)
                var todayStr = DateTime.Now.ToString("yyyyMMdd");
                var prefix = $"QC-{todayStr}-";
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
                    Remark = input.Remark,

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
        }// GET: api/PurchaseRequest/ViewFile/{id}
        [HttpGet("ViewFile/{id}")]
        public async Task<IActionResult> ViewFile(int id)
        {
            try
            {
                // 1. ค้นหา Quotation พร้อมข้อมูล AttachmentFile
                // สำคัญ: ต้อง .Include(q => q.AttachmentFile) เพื่อดึงข้อมูล Binary ออกมาด้วย
                var quotation = await _context.Quotations
                    .Include(q => q.AttachmentFile)
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (quotation == null)
                    return NotFound("ไม่พบข้อมูลเอกสาร");

                // 2. ตรวจสอบและส่งไฟล์กลับ

                // กรณี A: มีข้อมูลใน Database (AttachmentFile)
                if (quotation.AttachmentFile != null && quotation.AttachmentFile.Data != null)
                {
                    // การ Return แบบนี้ Browser จะพยายาม "เปิดดู (Preview)" ถ้าเป็น PDF หรือรูปภาพ
                    return File(quotation.AttachmentFile.Data, quotation.AttachmentFile.ContentType);

                    // หมายเหตุ: ถ้าต้องการบังคับ "ดาวน์โหลด" ให้เพิ่มพารามิเตอร์ชื่อไฟล์เข้าไปตัวที่ 3
                    // return File(quotation.AttachmentFile.Data, quotation.AttachmentFile.ContentType, quotation.FileName);
                }

                // กรณี B: (Fallback) ไฟล์เก่าที่เก็บใน Disk (เผื่อระบบเก่า)
                if (!string.IsNullOrEmpty(quotation.FilePath))
                {
                    // ตัด path ส่วนเกินออก (ถ้ามี) เพื่อหา path จริงใน wwwroot
                    // ตัวอย่าง: ถ้าใน DB เก็บ "uploads/2023/file.pdf"
                    var fullPath = Path.Combine(_env.WebRootPath, quotation.FilePath);

                    if (System.IO.File.Exists(fullPath))
                    {
                        var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
                        var contentType = quotation.ContentType ?? "application/pdf";
                        return File(fileBytes, contentType);
                    }
                }

                return NotFound("ไม่พบไฟล์ต้นฉบับในระบบ");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }
        private async Task HandleFileUploads(CreatePurchaseRequestDto input, PurchaseRequest pr)
        {
            if (input.Attachments == null || input.Attachments.Count == 0) return;

            // ส่วน Deserialize Metadata (เหมือนเดิม)
            var metaDataList = JsonSerializer.Deserialize<List<QuotationItemDto>>(
                input.QuotationsJson ?? "[]",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            foreach (var file in input.Attachments)
            {
                if (file.Length > 0)
                {
                    // 1. อ่านไฟล์เป็น Byte Array (เพื่อเก็บลง DB)
                    byte[] fileData;
                    using (var memoryStream = new MemoryStream())
                    {
                        await file.CopyToAsync(memoryStream);
                        fileData = memoryStream.ToArray();
                    }

                    // 2. สร้าง Entity AttachmentFile
                    var attachment = new AttachmentFile
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        Data = fileData
                        // CreatedAt, CreatedBy จะถูกจัดการโดย DbContext.SaveChanges
                    };

                    // 3. เตรียม Metadata (Document Type)
                    var meta = metaDataList?.FirstOrDefault(m => m.FileName == file.FileName)
                               ?? new QuotationItemDto { DocumentTypeId = 10 };

                    // 4. สร้าง Quotation และเชื่อมโยง Relation
                    var quotation = new Quotation
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        DocumentTypeId = meta.DocumentTypeId,

                        // ไม่ต้องใช้ FilePath แล้ว (หรืออาจใส่เป็น string.Empty ถ้า field นี้บังคับ)
                        FilePath = "Database",

                        // *** เชื่อมโยง object AttachmentFile ที่นี่ ***
                        // เมื่อ save changes EF Core จะบันทึก AttachmentFile ก่อนแล้วนำ ID มาใส่ให้ Quotation อัตโนมัติ
                        AttachmentFile = attachment
                    };

                    pr.Quotations.Add(quotation);
                }
            }
        }
    }
}