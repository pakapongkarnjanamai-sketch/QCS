
using Microsoft.AspNetCore.Http;
using QCS.Infrastructure.Data;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace QCS.Application.Services
{
    public interface IRequestService
    {
        // ==========================================================
        // 🔍 QUERY (สำหรับ DevExtreme & Detail)
        // ==========================================================
        // ใน Interface IRequestService
        Task<PurchaseRequestDetailDto?> GetByCodeAsync(string code);


        /// <summary>
        /// ดึงข้อมูลรายละเอียดเอกสารตาม ID (รวม Quotations และ History)
        /// </summary>
        Task<PurchaseRequestDetailDto?> GetByIdAsync(int id);

        // ==========================================================
        // 📋 SPECIFIC LISTS (สำหรับหน้า Dashboard / List)
        // ==========================================================

        /// <summary>
        /// รายการเอกสารที่ "ฉันเป็นคนสร้าง"
        /// </summary>
        Task<IEnumerable<object>> GetMyRequestsAsync();

        /// <summary>
        /// รายการเอกสารที่ "รอฉันอนุมัติ"
        /// </summary>
        Task<IEnumerable<object>> GetPendingApprovalsAsync();

        /// <summary>
        /// รายการเอกสารที่ "อนุมัติเสร็จสิ้นแล้ว"
        /// </summary>
        Task<IEnumerable<object>> GetApprovedListAsync();

        // ==========================================================
        // 💾 COMMANDS (Create / Update / Delete)
        // ==========================================================

        /// <summary>
        /// สร้างเอกสารใหม่
        /// </summary>
        /// <param name="input">ข้อมูลจาก Form</param>
        /// <param name="isSubmit">true = ส่งอนุมัติเลย, false = บันทึกร่าง</param>
        Task<PurchaseRequest> CreateAsync(CreatePurchaseRequestDto input, bool isSubmit);

        /// <summary>
        /// แก้ไขเอกสาร (เฉพาะสถานะ Draft หรือโดน Reject กลับมาแก้)
        /// </summary>
        /// <param name="input">ข้อมูลจาก Form</param>
        /// <param name="isSubmit">true = ส่งอนุมัติเลย, false = บันทึกร่าง</param>
        Task UpdateAsync(UpdatePurchaseRequestDto input, bool isSubmit);

        /// <summary>
        /// ลบเอกสาร (Soft Delete)
        /// </summary>
        Task DeleteAsync(int id);

        // ==========================================================
        // 📂 FILE HANDLING
        // ==========================================================

        /// <summary>
        /// ดึงไฟล์แนบเพื่อ Download หรือ View
        /// </summary>
        Task<AttachmentResultDto?> GetAttachmentAsync(int fileId);
    }
    public class RequestService : IRequestService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly WorkflowService _workflowService;
        private readonly IWebHostEnvironment _env;

        public RequestService(
            AppDbContext context,
            ICurrentUserService currentUserService,
            WorkflowService workflowService,
            IWebHostEnvironment env)
        {
            _context = context;
            _currentUserService = currentUserService;
            _workflowService = workflowService;
            _env = env;
        }

        // ==========================================================
        // 🔍 QUERY METHODS
        // ==========================================================


        // ใน Class RequestService
        public async Task<PurchaseRequestDetailDto?> GetByCodeAsync(string code)
        {
            // ค้นหา ID จาก Code ก่อน
            var id = await _context.PurchaseRequests
                .AsNoTracking()
                .Where(r => r.Code == code)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (id == 0) return null;

            // Reuse Logic เดิมของ GetByIdAsync เพื่อให้ Return Data Structure เดียวกันเป๊ะ
            return await GetByIdAsync(id);
        }

        public async Task<PurchaseRequestDetailDto?> GetByIdAsync(int id)
        {
            var request = await _context.PurchaseRequests
                .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                .Include(r => r.ApprovalSteps)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return null;

            // 1. ดึง Workflow Template เพื่อเช็คสิทธิ์
            var workflowRoute = await _workflowService.GetWorkflowRouteDetailAsync(1); // ควรย้าย 1 ไป Config

            // 2. Merge ข้อมูล History ลงใน Route (เพื่อแสดง Timeline)
            if (workflowRoute?.Steps != null)
            {
                foreach (var routeStep in workflowRoute.Steps)
                {
                    var actualStep = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == routeStep.SequenceNo);
                    if (actualStep != null)
                    {
                        routeStep.Status = actualStep.Status;
                        routeStep.ActionDate = actualStep.ActionDate;
                        routeStep.Comment = actualStep.Comment;
                        routeStep.ApproverName = actualStep.ApproverName;
                        routeStep.ApproverNId = actualStep.ApproverNId;
                    }

                    // Mark Current User Assignment
                    if (routeStep.Assignments != null)
                    {
                        foreach (var assign in routeStep.Assignments)
                        {
                            if (string.Equals(assign.NId, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
                            {
                                assign.IsCurrentUser = true;
                            }
                        }
                    }
                }
            }

            // 3. คำนวณ Permission
            bool canApprove = false;
            bool canReject = false;
            bool canEdit = request.Status == (int)RequestStatus.Draft;

            if (request.Status == (int)RequestStatus.Pending && workflowRoute?.Steps != null)
            {
                var currentStepConfig = workflowRoute.Steps.FirstOrDefault(s => s.SequenceNo == request.CurrentStepId);
                if (currentStepConfig?.Assignments != null && currentStepConfig.Assignments.Any(a => a.IsCurrentUser))
                {
                    canApprove = true;
                    canReject = true;
                }
            }

            // 4. Map DTO
            return new PurchaseRequestDetailDto
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
        }

        // ==========================================================
        // 📋 LIST METHODS
        // ==========================================================

        public async Task<IEnumerable<object>> GetMyRequestsAsync()
        {
            // TODO: ถ้ามี Field CreatedBy ให้ Uncomment
            // var userId = _currentUserService.UserId;
            return await _context.PurchaseRequests
                //.Where(r => r.CreatedBy == userId) 
                .Where(r => r.Status != (int)RequestStatus.Approved)
                .OrderByDescending(r => r.RequestDate)
                .Select(r => new
                {
                    r.Id,
                    r.Code,
                    r.Title,
                    r.RequestDate,
                    r.Status,
                    r.CurrentStepId,
                    r.VendorName
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<object>> GetPendingApprovalsAsync()
        {
            var userId = _currentUserService.UserId;
            var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);

            if (routeData?.Steps == null) return new List<object>();

            var myStepSequences = routeData.Steps
                .Where(s => s.Assignments != null &&
                            s.Assignments.Any(a => string.Equals(a.NId, userId, StringComparison.OrdinalIgnoreCase)))
                .Select(s => s.SequenceNo)
                .ToList();

            if (!myStepSequences.Any()) return new List<object>();

            return await _context.PurchaseRequests
                .Where(r => r.Status == (int)RequestStatus.Pending &&
                            myStepSequences.Contains(r.CurrentStepId))
                .OrderByDescending(r => r.RequestDate)
                .Select(r => new
                {
                    r.Id,
                    r.Code,
                    r.Title,
                    r.RequestDate,
                    r.Status,
                    r.CurrentStepId,
                    r.VendorName,
                    RequesterName = r.ApprovalSteps
                        .Where(s => s.Sequence == 1)
                        .Select(s => s.ApproverName)
                        .FirstOrDefault() ?? "Unknown"
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<object>> GetApprovedListAsync()
        {
            return await _context.PurchaseRequests
                .Where(r => r.Status == (int)RequestStatus.Approved ||
                            r.Status == (int)RequestStatus.Completed)
                .OrderByDescending(r => r.RequestDate)
                .Select(r => new
                {
                    r.Id,
                    r.Code,
                    r.Title,
                    r.RequestDate,
                    r.Status,
                    r.VendorName,
                    r.Remark
                })
                .ToListAsync();
        }

        // ==========================================================
        // 💾 COMMAND METHODS (Create / Update / Delete)
        // ==========================================================

        public async Task<PurchaseRequest> CreateAsync(CreatePurchaseRequestDto input, bool isSubmit)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Prepare Workflow & Doc No
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);
                if (routeData?.Steps == null) throw new Exception("Workflow definition not found");

                var sortedSteps = routeData.Steps.OrderBy(s => s.SequenceNo).ToList();
                var newDocNo = await GenerateDocNoAsync();

                // 2. Determine Status
                int currentStepId = 1;
                int docStatus = isSubmit ? (int)RequestStatus.Pending : (int)RequestStatus.Draft;

                if (isSubmit)
                {
                    var nextStep = sortedSteps.FirstOrDefault(s => s.SequenceNo > 1);
                    currentStepId = nextStep != null ? nextStep.SequenceNo : 99;
                    if (currentStepId == 99) docStatus = (int)RequestStatus.Approved;
                }

                // 3. Create Entity
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

                // 4. Create Approval Steps History
                foreach (var step in sortedSteps)
                {
                    int stepStatus = (int)RequestStatus.Draft;
                    string? approverNId = null;
                    string? approverName = null;
                    string? comment = null;
                    DateTime? actionDate = null;

                    if (step.SequenceNo == 1) // Step 1 = Requester
                    {
                        if (isSubmit)
                        {
                            stepStatus = (int)RequestStatus.Approved;
                            actionDate = DateTime.Now;
                            approverNId = _currentUserService.UserId;
                            approverName = await GetApproverNameAsync(1, _currentUserService.UserId);
                            comment = input.Comment;
                        }
                        else
                        {
                            stepStatus = (int)RequestStatus.Pending; // Draft mode
                        }
                    }
                    else if (step.SequenceNo == 2 && isSubmit)
                    {
                        stepStatus = (int)RequestStatus.Pending; // Waiting for Step 2
                    }

                    pr.ApprovalSteps.Add(new ApprovalStep
                    {
                        Sequence = step.SequenceNo,
                        StepName = step.StepName,
                        Status = stepStatus,
                        ActionDate = actionDate,
                        ApproverNId = approverNId,
                        ApproverName = approverName,
                        Comment = comment
                    });
                }

                // 5. Handle Files
                await HandleFileUploadsAsync(input, pr);

                await _context.PurchaseRequests.AddAsync(pr);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return pr;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateAsync(UpdatePurchaseRequestDto input, bool isSubmit)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var pr = await _context.PurchaseRequests
                    .Include(r => r.Quotations)
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == input.Id);

                if (pr == null) throw new KeyNotFoundException("Document not found");
                if (pr.Status != (int)RequestStatus.Draft) throw new InvalidOperationException("Cannot edit non-draft document");

                // Update Fields
                pr.Title = input.Title;
                pr.VendorId = input.VendorId;
                pr.VendorName = input.VendorName;
                pr.ValidFrom = input.ValidFrom;
                pr.ValidUntil = input.ValidUntil;
                pr.Remark = input.Remark;

                if (isSubmit)
                {
                    pr.Status = (int)RequestStatus.Pending;

                    // Update Step 1 (Requester) -> Approved
                    var step1 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 1);
                    if (step1 != null)
                    {
                        step1.Status = (int)RequestStatus.Approved;
                        step1.ActionDate = DateTime.Now;
                        step1.ApproverNId = _currentUserService.UserId;
                        step1.ApproverName = await GetApproverNameAsync(1, _currentUserService.UserId);
                        step1.Comment = input.Comment;
                    }

                    // Activate Step 2 -> Pending
                    var step2 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 2);
                    if (step2 != null)
                    {
                        step2.Status = (int)RequestStatus.Pending;
                        step2.ApproverNId = null; // Clear old data if rejected
                        step2.ApproverName = null;
                        pr.CurrentStepId = 2;
                    }
                }

                // Handle Files Updates (Metadata)
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

                // Handle Files Delete
                if (!string.IsNullOrEmpty(input.DeletedFileIds))
                {
                    var ids = input.DeletedFileIds.Split(',').Select(int.Parse).ToList();
                    var toRemove = pr.Quotations.Where(q => ids.Contains(q.Id)).ToList();
                    _context.Quotations.RemoveRange(toRemove);
                }

                // Handle New Files
                await HandleFileUploadsAsync(input, pr);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            var pr = await _context.PurchaseRequests.FindAsync(id);
            if (pr != null)
            {
                // Soft Delete or Hard Delete based on policy
                _context.PurchaseRequests.Remove(pr);
                await _context.SaveChangesAsync();
            }
        }

        // ==========================================================
        // 📂 FILE HANDLING
        // ==========================================================

        public async Task<AttachmentResultDto?> GetAttachmentAsync(int fileId)
        {
            var q = await _context.Quotations.Include(x => x.AttachmentFile).FirstOrDefaultAsync(x => x.Id == fileId);
            if (q == null) return null;

            // 1. Try DB
            if (q.AttachmentFile?.Data != null)
            {
                return new AttachmentResultDto
                {
                    Data = q.AttachmentFile.Data,
                    ContentType = q.AttachmentFile.ContentType ?? "application/octet-stream",
                    FileName = q.FileName
                };
            }

            // 2. Try Disk (Legacy Support)
            if (!string.IsNullOrEmpty(q.FilePath) && q.FilePath != "Database")
            {
                var path = Path.Combine(_env.WebRootPath, q.FilePath);
                if (System.IO.File.Exists(path))
                {
                    return new AttachmentResultDto
                    {
                        Data = await System.IO.File.ReadAllBytesAsync(path),
                        ContentType = q.ContentType ?? "application/octet-stream",
                        FileName = q.FileName
                    };
                }
            }

            return null;
        }

        // ==========================================================
        // 🛠 PRIVATE HELPERS
        // ==========================================================

        private async Task<string> GenerateDocNoAsync()
        {
            var todayStr = DateTime.Now.ToString("yyyyMMdd");
            var prefix = $"QC-{todayStr}-";
            var countToday = await _context.PurchaseRequests.CountAsync(x => x.Code.StartsWith(prefix));
            return $"{prefix}{(countToday + 1):D3}";
        }

        private async Task<string> GetApproverNameAsync(int routeId, string nId)
        {
            var name = await _workflowService.GetEmployeeNameFromWorkflowAsync(routeId, nId);
            return !string.IsNullOrEmpty(name) ? name : nId;
        }

        private async Task HandleFileUploadsAsync(dynamic input, PurchaseRequest pr)
        {
            // Reflection to get Attachments from both Create and Update DTOs
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
                    int typeId = meta != null ? meta.DocumentTypeId : 10; // Default 10 = Other

                    pr.Quotations.Add(new Quotation
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        DocumentTypeId = typeId,
                        FilePath = "Database",
                        AttachmentFile = attachment
                    });
                }
            }
        }
    }

}