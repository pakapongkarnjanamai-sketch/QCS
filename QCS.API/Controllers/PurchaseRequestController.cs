using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Services;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;
using System.Security.Claims;
using System.Text.Json;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PurchaseRequestController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly WorkflowService _workflowService;

        public PurchaseRequestController(
            AppDbContext context,
            IWebHostEnvironment env,
            WorkflowService workflowService)
        {
            _context = context;
            _env = env;
            _workflowService = workflowService;
        }

        // Helper: ดึง User ID (nId) ของคนที่ Login อยู่
        private string CurrentUserNId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                         ?? User.FindFirst("nId")?.Value
                                         ?? "SYSTEM";

        // ==========================================================
        // 🔍 GET DETAIL (Main Endpoint)
        // ==========================================================
        [HttpGet("Detail/{id}")]
        public async Task<IActionResult> GetRequestDetail(int id)
        {
            try
            {
                // 1. ดึงข้อมูล PR และ History
                var request = await _context.PurchaseRequests
                    .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile) // Include เพื่อเช็คว่ามีไฟล์ไหม
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request == null) return NotFound("ไม่พบข้อมูลเอกสาร");

                // 2. ดึง Workflow Route Template (จาก API)
                // เพื่อเอาโครงสร้างว่า Step 1, 2, 3 คือใคร (Plan)
                var workflowRoute = await _workflowService.GetWorkflowRouteDetailAsync(1);

                // 3. Merge ข้อมูล: เอา History (Actual) ไปแปะทับ Route (Plan)
                if (workflowRoute != null && workflowRoute.Steps != null)
                {
                    foreach (var routeStep in workflowRoute.Steps)
                    {
                        // หาข้อมูลการอนุมัติจริงใน DB ที่ Sequence ตรงกัน
                        var actualStep = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == routeStep.SequenceNo);

                        if (actualStep != null)
                        {
                            // Map สถานะจริงใส่ลงไป
                            routeStep.Status = actualStep.Status;
                            routeStep.ActionDate = actualStep.ActionDate;
                            routeStep.Comment = actualStep.Comment;

                            // Map ชื่อคนอนุมัติจริง (ถ้ามี)
                            if (!string.IsNullOrEmpty(actualStep.ApproverName))
                            {
                                routeStep.ApproverName = actualStep.ApproverName;
                            }
                        }

                        // Map Flag 'IsCurrentUser' สำหรับ Assignments
                        if (routeStep.Assignments != null)
                        {
                            foreach (var assign in routeStep.Assignments)
                            {
                                if (string.Equals(assign.NId, CurrentUserNId, StringComparison.OrdinalIgnoreCase))
                                {
                                    assign.IsCurrentUser = true;
                                }
                            }
                        }
                    }
                }

                // 4. คำนวณ Permission (สำหรับปุ่มกด)
                bool canApprove = false;
                bool canReject = false;
                bool canEdit = false;

                // Logic Edit: แก้ได้ถ้าเป็น Draft หรือ Rejected และเป็นคนสร้าง
                // (สมมติว่าไม่มี field CreatedBy ใน Model ให้ใช้ Logic อื่นแทน หรือเพิ่ม field)
                // ในที่นี้สมมติว่าใครสร้างก็ได้แก้ได้ถ้ายังไม่ส่ง (ปรับตาม Business Logic จริง)
                if (request.Status == (int)RequestStatus.Draft || request.Status == (int)RequestStatus.Rejected)
                {
                    canEdit = true;
                }

                // Logic Approve: ต้องเป็น Pending + งานอยู่ที่ Step ปัจจุบัน + User ตรงกับ Assignment
                if (request.Status == (int)RequestStatus.Pending && workflowRoute != null)
                {
                    var currentStepConfig = workflowRoute.Steps
                        .FirstOrDefault(s => s.SequenceNo == request.CurrentStepId);

                    if (currentStepConfig != null && currentStepConfig.Assignments != null)
                    {
                        // เช็คว่า User ปัจจุบันอยู่ในรายการผู้รับผิดชอบหรือไม่
                        if (currentStepConfig.Assignments.Any(a => a.IsCurrentUser))
                        {
                            canApprove = true;
                            canReject = true;
                        }
                    }
                }

                // 5. Map DTO
                var dto = new PurchaseRequestDetailDto
                {
                    PurchaseRequestId = request.Id,
                    DocumentNo = request.Code, // หรือ request.DocumentNo
                    Title = request.Title,
                    RequestDate = request.RequestDate,
                    Status = request.Status.ToString(),
                    CurrentStepId = request.CurrentStepId,
                    VendorId = request.VendorId,
                    VendorName = request.VendorName,
                    ValidFrom = request.ValidFrom,
                    ValidUntil = request.ValidUntil,
                    Remark = request.Remark,

                    Quotations = request.Quotations.Select(q => new QuotationItemDto
                    {
                        Id = q.Id,
                        DocumentTypeId = q.DocumentTypeId,
                        OriginalFileName = q.FileName, // หรือ q.OriginalFileName
                        FilePath = q.FilePath
                    }).ToList(),

                    Permissions = new PermissionDto
                    {
                        CanApprove = canApprove,
                        CanReject = canReject,
                        CanEdit = canEdit
                    },

                    WorkflowRoute = workflowRoute // ส่ง Route ที่ Merge แล้วกลับไป
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        // ==========================================================
        // 💾 SAVE & SUBMIT (Create)
        // ==========================================================
        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromForm] CreatePurchaseRequestDto input)
        {
            return await ProcessCreation(input, isSubmit: false);
        }

        [HttpPost("Submit")]
        public async Task<IActionResult> Submit([FromForm] CreatePurchaseRequestDto input)
        {
            return await ProcessCreation(input, isSubmit: true);
        }

        // ==========================================================
        // 📝 UPDATE & SUBMIT (Edit)
        // ==========================================================
        [HttpPost("Update")]
        public async Task<IActionResult> Update([FromForm] UpdatePurchaseRequestDto input)
        {
            return await ProcessUpdate(input, isSubmit: false);
        }

        [HttpPost("SubmitUpdate")]
        public async Task<IActionResult> SubmitUpdate([FromForm] UpdatePurchaseRequestDto input)
        {
            return await ProcessUpdate(input, isSubmit: true);
        }

        // ==========================================================
        // 🛠 CORE LOGIC: PROCESS CREATION
        // ==========================================================
        private async Task<IActionResult> ProcessCreation(CreatePurchaseRequestDto input, bool isSubmit)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. ดึง Workflow Template
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1); // ID=1 (Fixed or from Input)
                if (routeData == null || routeData.Steps == null)
                    return BadRequest("ไม่สามารถดึงข้อมูล Workflow Route ได้");

                var sortedSteps = routeData.Steps.OrderBy(s => s.SequenceNo).ToList();

                // 2. Running Number
                var todayStr = DateTime.Now.ToString("yyyyMMdd");
                var prefix = $"QC-{todayStr}-";
                var countToday = await _context.PurchaseRequests.CountAsync(x => x.Code.StartsWith(prefix));
                var newDocNo = $"{prefix}{(countToday + 1):D3}";

                // 3. กำหนด Status
                int currentStepId = 1;
                int docStatus = isSubmit ? (int)RequestStatus.Pending : (int)RequestStatus.Draft;

                // กรณี Submit: Step 1 (Purchaser) เสร็จเลย -> ไป Step 2
                if (isSubmit)
                {
                    var nextStep = sortedSteps.FirstOrDefault(s => s.SequenceNo > 1);
                    currentStepId = nextStep != null ? nextStep.SequenceNo : 99;
                    if (currentStepId == 99) docStatus = (int)RequestStatus.Approved;
                }

                // 4. Create Header
                var pr = new PurchaseRequest
                {
                    Code = newDocNo,
                    Title = input.Title,
                    RequestDate = DateTime.Now,
                    Status = docStatus,
                    CurrentStepId = currentStepId,
                    VendorId = input.VendorId,
                    VendorName = input.VendorName,
                    ValidFrom = input.ValidFrom,
                    ValidUntil = input.ValidUntil,
                    Remark = input.Remark,
                    ApprovalSteps = new List<ApprovalStep>(),
                    Quotations = new List<Quotation>()
                };

                // 5. Create Approval Steps & Assign Approvers
                foreach (var step in sortedSteps)
                {
                    int stepStatus = (int)RequestStatus.Draft;
                    DateTime? actionDate = null;
                    string? approverNId = null;
                    string? approverName = null;

                    // Logic Status
                    if (step.SequenceNo == 1) // Purchaser (คนสร้าง)
                    {
                        if (isSubmit) { stepStatus = (int)RequestStatus.Approved; actionDate = DateTime.Now; }
                        else { stepStatus = (int)RequestStatus.Pending; }

                        // Step 1 คือคนปัจจุบันเสมอ
                        approverNId = CurrentUserNId;
                    }
                    else if (step.SequenceNo == 2 && isSubmit)
                    {
                        stepStatus = (int)RequestStatus.Pending; // มารอที่คนนี้
                    }

                    // Logic Assignment (ดึงจาก Template มาใส่ DB เพื่อให้ ApprovalController เช็คได้)
                    if (step.Assignments != null && step.Assignments.Any())
                    {
                        // สมมติเอาคนแรกมาเป็น Assignee หลัก (หรือจะเก็บเป็น List แยกก็ได้ แต่ Model ApprovalStep รับ 1 คน)
                        var assignee = step.Assignments.FirstOrDefault();
                        if (assignee != null && step.SequenceNo != 1) // Step 1 ใช้ CurrentUser
                        {
                            approverNId = assignee.NId;
                            approverName = assignee.EmployeeName;
                        }
                    }

                    pr.ApprovalSteps.Add(new ApprovalStep
                    {
                        Sequence = step.SequenceNo,
                        StepName = step.StepName, // ใช้ StepName แทน Role
                        Status = stepStatus,
                        ActionDate = actionDate,
                        ApproverNId = approverNId,
                        ApproverName = approverName
                    });
                }

                // 6. Upload Files
                await HandleFileUploads(input, pr);

                _context.PurchaseRequests.Add(pr);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, id = pr.Id, docNo = pr.Code });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        // ==========================================================
        // 🛠 CORE LOGIC: PROCESS UPDATE
        // ==========================================================
        private async Task<IActionResult> ProcessUpdate(UpdatePurchaseRequestDto input, bool isSubmit)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var pr = await _context.PurchaseRequests
                    .Include(r => r.Quotations)
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == input.Id);

                if (pr == null) return NotFound("ไม่พบข้อมูล");

                // Update Header
                pr.Title = input.Title;
                pr.VendorId = input.VendorId;
                pr.VendorName = input.VendorName;
                pr.ValidFrom = input.ValidFrom;
                pr.ValidUntil = input.ValidUntil;
                pr.Remark = input.Remark;

                // Update Workflow Status
                if (isSubmit)
                {
                    pr.Status = (int)RequestStatus.Pending;

                    // Step 1 -> Approved
                    var step1 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 1);
                    if (step1 != null)
                    {
                        step1.Status = (int)RequestStatus.Approved;
                        step1.ActionDate = DateTime.Now;
                        step1.ApproverNId = CurrentUserNId; // อัปเดตคนกด Submit ล่าสุด
                    }

                    // Step 2 -> Pending
                    var step2 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 2);
                    if (step2 != null)
                    {
                        step2.Status = (int)RequestStatus.Pending;
                        pr.CurrentStepId = 2;
                    }
                }

                // Handle Files: Update Type
                if (!string.IsNullOrEmpty(input.UpdatedQuotationsJson))
                {
                    var updates = JsonSerializer.Deserialize<List<QuotationItemDto>>(input.UpdatedQuotationsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (updates != null)
                    {
                        foreach (var item in updates)
                        {
                            var f = pr.Quotations.FirstOrDefault(q => q.Id == item.Id);
                            if (f != null) f.DocumentTypeId = item.DocumentTypeId;
                        }
                    }
                }

                // Handle Files: Delete
                if (!string.IsNullOrEmpty(input.DeletedFileIds))
                {
                    var ids = input.DeletedFileIds.Split(',').Select(int.Parse).ToList();
                    var toRemove = pr.Quotations.Where(q => ids.Contains(q.Id)).ToList();
                    _context.Quotations.RemoveRange(toRemove);
                }

                // Handle Files: Add New
                await HandleFileUploads(input, pr);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        // ==========================================================
        // 📂 FILE UTILS
        // ==========================================================
        private async Task HandleFileUploads(dynamic input, PurchaseRequest pr)
        {
            // dynamic input รองรับทั้ง CreateDto และ UpdateDto
            // โดยต้องมี Property: Attachments (หรือ NewAttachments) และ QuotationsJson

            var files = (input.GetType().GetProperty("Attachments")?.GetValue(input) as List<IFormFile>)
                        ?? (input.GetType().GetProperty("NewAttachments")?.GetValue(input) as List<IFormFile>);

            if (files == null || files.Count == 0) return;

            var metaJson = input.QuotationsJson as string;
            var metaList = string.IsNullOrEmpty(metaJson)
                ? new List<QuotationItemDto>()
                : JsonSerializer.Deserialize<List<QuotationItemDto>>(metaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    // 1. อ่านไฟล์เป็น Byte
                    byte[] fileData;
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        fileData = ms.ToArray();
                    }

                    // 2. สร้าง AttachmentFile (เก็บเนื้อไฟล์)
                    var attachment = new AttachmentFile
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        Data = fileData
                    };

                    // 3. หา DocumentType
                    var meta = metaList?.FirstOrDefault(m => m.FileName == file.FileName);
                    int typeId = meta != null ? meta.DocumentTypeId : 10;

                    // 4. สร้าง Quotation (เก็บ Metadata)
                    var quotation = new Quotation
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        DocumentTypeId = typeId,
                        FilePath = "Database", // Mark ว่าเก็บใน DB
                        AttachmentFile = attachment
                    };

                    pr.Quotations.Add(quotation);
                }
            }
        }

        [HttpGet("ViewFile/{id}")]
        public async Task<IActionResult> ViewFile(int id)
        {
            var q = await _context.Quotations.Include(x => x.AttachmentFile).FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            if (q.AttachmentFile != null && q.AttachmentFile.Data != null)
            {
                return File(q.AttachmentFile.Data, q.AttachmentFile.ContentType ?? "application/pdf");
            }

            // Fallback for old system files (if any)
            if (!string.IsNullOrEmpty(q.FilePath) && q.FilePath != "Database")
            {
                var path = Path.Combine(_env.WebRootPath, q.FilePath);
                if (System.IO.File.Exists(path))
                    return PhysicalFile(path, q.ContentType ?? "application/pdf");
            }

            return NotFound("File content missing");
        }
    }
}