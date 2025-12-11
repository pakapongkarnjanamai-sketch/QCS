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

        // ==========================================================
        // 🔑 HELPER: CURRENT USER
        // ==========================================================
        private string CurrentUserNId
        {
            get
            {
                var fullIdentityName = User.Identity?.Name; // เช่น "DOMAIN\n4734"
                if (string.IsNullOrEmpty(fullIdentityName)) return "SYSTEM";

                var parts = fullIdentityName.Split('\\');
                // เอาส่วนข้างหลัง \ ถ้ามี หรือเอาทั้งหมดถ้าไม่มี
                return parts.Length > 1 ? parts[1] : parts[0];
            }
        }

        // ==========================================================
        // 🔍 GET DETAIL
        // ==========================================================
        [HttpGet("Detail/{id}")]
        public async Task<IActionResult> GetRequestDetail(int id)
        {
            try
            {
                // 1. ดึงข้อมูล PR และ History
                var request = await _context.PurchaseRequests
                    .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request == null) return NotFound("ไม่พบข้อมูลเอกสาร");

                // 2. ดึง Workflow Template (Plan)
                var workflowRoute = await _workflowService.GetWorkflowRouteDetailAsync(1); // ID 1

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

                            // Map ข้อมูลผู้กระทำ (Actual Approver)
                            if (!string.IsNullOrEmpty(actualStep.ApproverName))
                            {
                                routeStep.ApproverName = actualStep.ApproverName;
                            }
                            // ส่ง NId ของคนทำจริงไปด้วย
                            if (!string.IsNullOrEmpty(actualStep.ApproverNId))
                            {
                                routeStep.ApproverNId = actualStep.ApproverNId;
                            }
                        }

                        // Map Flag 'IsCurrentUser' สำหรับ Assignments (เพื่อ Highlight ว่าตาใคร)
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

                // 4. คำนวณ Permission
                bool canApprove = false;
                bool canReject = false;
                bool canEdit = false;

                // Edit: แก้ได้ถ้าเป็น Draft เท่านั้น (Rejected หรือ Completed แก้ไม่ได้)
                if (request.Status == (int)RequestStatus.Draft)
                {
                    canEdit = true;
                }

                // Approve: ต้องเป็น Pending + งานอยู่ที่ Step นี้ + User ตรงกับ Assignment ใน Workflow Plan
                if (request.Status == (int)RequestStatus.Pending && workflowRoute != null)
                {
                    var currentStepConfig = workflowRoute.Steps
                        .FirstOrDefault(s => s.SequenceNo == request.CurrentStepId);

                    if (currentStepConfig != null && currentStepConfig.Assignments != null)
                    {
                        if (currentStepConfig.Assignments.Any(a => a.IsCurrentUser)) // ใช้ IsCurrentUser ที่ Set ไว้ข้างบน
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
                    DocumentNo = request.Code,
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
                        OriginalFileName = q.FileName,
                        FilePath = q.FilePath
                    }).ToList(),

                    Permissions = new PermissionDto
                    {
                        CanApprove = canApprove,
                        CanReject = canReject,
                        CanEdit = canEdit
                    },

                    WorkflowRoute = workflowRoute
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        // ==========================================================
        // 📋 LIST MY REQUESTS
        // ==========================================================
        [HttpGet("MyRequests")]
        public async Task<IActionResult> GetMyRequests()
        {
            try
            {
                // ถ้ามี field CreatedBy ให้ Uncomment
                var requests = await _context.PurchaseRequests
                    // .Where(r => r.CreatedBy == CurrentUserNId) 
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
                return StatusCode(500, ex.Message);
            }
        }

        // ==========================================================
        // 💾 ACTIONS
        // ==========================================================
        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromForm] CreatePurchaseRequestDto input)
            => await ProcessCreation(input, isSubmit: false);

        [HttpPost("Submit")]
        public async Task<IActionResult> Submit([FromForm] CreatePurchaseRequestDto input)
            => await ProcessCreation(input, isSubmit: true);

        [HttpPost("Update")]
        public async Task<IActionResult> Update([FromForm] UpdatePurchaseRequestDto input)
            => await ProcessUpdate(input, isSubmit: false);

        [HttpPost("SubmitUpdate")]
        public async Task<IActionResult> SubmitUpdate([FromForm] UpdatePurchaseRequestDto input)
            => await ProcessUpdate(input, isSubmit: true);

        // ==========================================================
        // 🛠 CORE LOGIC: PROCESS CREATION (Submit = Approve Step 1)
        // ==========================================================
        private async Task<IActionResult> ProcessCreation(CreatePurchaseRequestDto input, bool isSubmit)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. ดึง Workflow Template
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);
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

                // 5. Create Approval Steps
                foreach (var step in sortedSteps)
                {
                    int stepStatus = (int)RequestStatus.Draft;
                    DateTime? actionDate = null;
                    string? approverNId = null;
                    string? approverName = null;

                    if (step.SequenceNo == 1) // Step 1: Purchaser (User ปัจจุบัน)
                    {
                        if (isSubmit)
                        {
                            // === Submit คือการ Approve Step 1 ===
                            stepStatus = (int)RequestStatus.Approved;
                            actionDate = DateTime.Now;

                            // บันทึกตัวตนผู้กระทำทันที
                            approverNId = CurrentUserNId;

                            // ไปดึงชื่อจริงจาก Workflow API มาบันทึก
                            var fetchedName = await _workflowService.GetEmployeeNameFromWorkflowAsync(1, CurrentUserNId);
                            approverName = !string.IsNullOrEmpty(fetchedName) ? fetchedName : CurrentUserNId;
                        }
                        else
                        {
                            stepStatus = (int)RequestStatus.Pending; // Save Draft
                        }
                    }
                    else if (step.SequenceNo == 2 && isSubmit)
                    {
                        stepStatus = (int)RequestStatus.Pending; // มารอที่ Step 2
                        // Approver ว่างไว้ เพราะยังไม่มีคนกด
                    }

                    pr.ApprovalSteps.Add(new ApprovalStep
                    {
                        Sequence = step.SequenceNo,
                        StepName = step.StepName,
                        Status = stepStatus,
                        ActionDate = actionDate,
                        ApproverNId = approverNId,
                        ApproverName = approverName
                    });
                }

                // 6. Handle Files
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
        // 🛠 CORE LOGIC: PROCESS UPDATE (Submit = Approve Step 1)
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

                // 🔒 VALIDATION: อนุญาตให้แก้ไขเฉพาะตอน Draft เท่านั้น
                if (pr.Status != (int)RequestStatus.Draft)
                {
                    return BadRequest("ไม่สามารถแก้ไขเอกสารได้ เนื่องจากสถานะปัจจุบันไม่ใช่ Draft (เอกสารอยู่ระหว่างการอนุมัติ หรือจบกระบวนการแล้ว)");
                }

                // Update Header
                pr.Title = input.Title;
                pr.VendorId = input.VendorId;
                pr.VendorName = input.VendorName;
                pr.ValidFrom = input.ValidFrom;
                pr.ValidUntil = input.ValidUntil;
                pr.Remark = input.Remark;

                if (isSubmit)
                {
                    pr.Status = (int)RequestStatus.Pending;

                    // Step 1: Update เป็น Approved + บันทึกผู้กระทำ
                    var step1 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 1);
                    if (step1 != null)
                    {
                        step1.Status = (int)RequestStatus.Approved;
                        step1.ActionDate = DateTime.Now;

                        // บันทึกคนกด Submit ล่าสุด
                        step1.ApproverNId = CurrentUserNId;
                        var fetchedName = await _workflowService.GetEmployeeNameFromWorkflowAsync(1, CurrentUserNId);
                        step1.ApproverName = !string.IsNullOrEmpty(fetchedName) ? fetchedName : CurrentUserNId;
                    }

                    // Step 2: เปิด Status Pending (เคลียร์คนทำเก่าออก กรณีถูก Reject กลับมา)
                    var step2 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 2);
                    if (step2 != null)
                    {
                        step2.Status = (int)RequestStatus.Pending;
                        step2.ApproverNId = null;
                        step2.ApproverName = null;
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
        // 📂 FILE HELPER
        // ==========================================================
        private async Task HandleFileUploads(dynamic input, PurchaseRequest pr)
        {
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
                    byte[] fileData;
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        fileData = ms.ToArray();
                    }

                    var attachment = new AttachmentFile
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        Data = fileData
                    };

                    var meta = metaList?.FirstOrDefault(m => m.FileName == file.FileName);
                    int typeId = meta != null ? meta.DocumentTypeId : 10;

                    var quotation = new Quotation
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        DocumentTypeId = typeId,
                        FilePath = "Database",
                        AttachmentFile = attachment
                    };

                    pr.Quotations.Add(quotation);
                }
            }
        }

        // ==========================================================
        // 📥 VIEW FILE
        // ==========================================================
        [HttpGet("ViewFile/{id}")]
        public async Task<IActionResult> ViewFile(int id)
        {
            var q = await _context.Quotations.Include(x => x.AttachmentFile).FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            if (q.AttachmentFile != null && q.AttachmentFile.Data != null)
            {
                return File(q.AttachmentFile.Data, q.AttachmentFile.ContentType ?? "application/pdf");
            }

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