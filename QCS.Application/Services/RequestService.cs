using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using QCS.Infrastructure.Data;
using System.Text.Json;

namespace QCS.Application.Services
{
    public interface IRequestService
    {
        IQueryable<RequestGridDto> GetMyRequestsQuery();
        Task<IQueryable<RequestGridDto>> GetMyTasksQueryAsync();
        IQueryable<RequestGridDto> GetApprovedListQuery();
        IQueryable<RequestGridDto> GetRejectedRequestsQuery();
        Task<RequestDetailDto?> GetByCodeAsync(string code);
        Task<RequestDetailDto?> GetByIdAsync(int id);
        Task<Request> CreateAsync(CreateRequestDto input, bool isSubmit);
        Task UpdateAsync(UpdateRequestDto input, bool isSubmit);
        Task DeleteAsync(int id);
        Task<AttachmentResultDto?> GetAttachmentAsync(int id);
    }

    public class RequestService : IRequestService
    {
        private readonly AppDbContext _context;
        private readonly WorkflowService _workflowService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IWebHostEnvironment _env;

        public RequestService(AppDbContext context, WorkflowService workflowService, ICurrentUserService currentUserService, IWebHostEnvironment env)
        {
            _context = context;
            _workflowService = workflowService;
            _currentUserService = currentUserService;
            _env = env;
        }

        public IQueryable<RequestGridDto> GetMyRequestsQuery()
        {
            return _context.Requests
                .AsNoTracking()
                .Where(r => r.CreatedBy == _currentUserService.UserId
                         && r.Status != (int)RequestStatus.Approved
                         && r.Status != (int)RequestStatus.Rejected)
                .Select(r => new RequestGridDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId,
                });
        }

        public IQueryable<RequestGridDto> GetRejectedRequestsQuery()
        {
            return _context.Requests
                .AsNoTracking()
                .Where(r => r.CreatedBy == _currentUserService.UserId && r.Status == (int)RequestStatus.Rejected)
                .Select(r => new RequestGridDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId
                });
        }

        public IQueryable<RequestGridDto> GetApprovedListQuery()
        {
            return _context.Requests
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Approved)
                .Select(r => new RequestGridDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId,
                });
        }

        public async Task<IQueryable<RequestGridDto>> GetMyTasksQueryAsync()
        {
            var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);
            var myStepSequences = new List<int>();

            if (routeData?.Steps != null)
            {
                myStepSequences = routeData.Steps
                    .Where(s => s.Assignments != null && s.Assignments.Any(a => a.NId == _currentUserService.UserId))
                    .Select(s => s.SequenceNo)
                    .ToList();
            }

            if (!myStepSequences.Any())
            {
                return _context.Requests.AsNoTracking().Where(r => false).Select(r => new RequestGridDto());
            }

            return _context.Requests
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Pending && myStepSequences.Contains(r.CurrentStepId))
                .Select(r => new RequestGridDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId,
                    RequesterName = r.ApprovalSteps
                            .Where(s => s.Sequence == 1)
                            .Select(s => s.ApproverName)
                            .FirstOrDefault() ?? "Unknown"
                });
        }

        public async Task<RequestDetailDto?> GetByIdAsync(int id)
        {
            var request = await _context.Requests
             .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
             .Include(r => r.ApprovalSteps)
             .AsNoTracking()
             .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return null;

            var workflowRoute = await _workflowService.GetWorkflowRouteDetailAsync(1);

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
                }
            }

            return new RequestDetailDto
            {
                RequestId = request.Id,
                DocumentNo = request.Code,
                Title = request.Title,
                RequestDate = request.RequestDate,
                Status = request.Status.ToString(),
                CurrentStepId = request.CurrentStepId,
                VendorCode = request.VendorCode,
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
                Permissions = _workflowService.GetPermissions(request, workflowRoute),
                WorkflowRoute = workflowRoute
            };
        }

        public async Task<RequestDetailDto?> GetByCodeAsync(string code)
        {
            var id = await _context.Requests
                .AsNoTracking()
                .Where(r => r.Code == code)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (id == 0) return null;
            return await GetByIdAsync(id);
        }

        public async Task<Request> CreateAsync(CreateRequestDto input, bool isSubmit)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(1);
                if (routeData?.Steps == null) throw new Exception("Workflow definition not found");

                var sortedSteps = routeData.Steps.OrderBy(s => s.SequenceNo).ToList();
                var newDocNo = await GenerateDocNoAsync();

                int currentStepId = 1;
                int docStatus = isSubmit ? (int)RequestStatus.Pending : (int)RequestStatus.Draft;

                if (isSubmit)
                {
                    var nextStep = sortedSteps.FirstOrDefault(s => s.SequenceNo > 1);
                    currentStepId = nextStep != null ? nextStep.SequenceNo : 99;
                    if (currentStepId == 99) docStatus = (int)RequestStatus.Approved;
                }

                var pr = new Request
                {
                    Code = newDocNo,
                    Title = input.Title,
                    RequestDate = DateTime.Now,
                    Status = docStatus,
                    CurrentStepId = currentStepId,
                    VendorCode = input.VendorCode,
                    VendorName = input.VendorName,
                    ValidFrom = input.ValidFrom,
                    ValidUntil = input.ValidUntil,
                    Remark = input.Remark
                };

                foreach (var step in sortedSteps)
                {
                    int stepStatus = (int)RequestStatus.Draft;
                    string? approverNId = null;
                    string? approverName = null;
                    string? comment = null;
                    DateTime? actionDate = null;

                    if (step.SequenceNo == 1)
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
                            stepStatus = (int)RequestStatus.Pending;
                        }
                    }
                    else if (step.SequenceNo == 2 && isSubmit)
                    {
                        stepStatus = (int)RequestStatus.Pending;
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

                await HandleFileUploadsAsync(input, pr);
                await _context.Requests.AddAsync(pr);
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

        public async Task UpdateAsync(UpdateRequestDto input, bool isSubmit)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var pr = await _context.Requests
                    .Include(r => r.Quotations)
                    .Include(r => r.ApprovalSteps)
                    .FirstOrDefaultAsync(r => r.Id == input.Id);

                if (pr == null) throw new KeyNotFoundException("Document not found");
                if (pr.Status != (int)RequestStatus.Draft) throw new InvalidOperationException("Cannot edit non-draft document");

                pr.Title = input.Title;
                pr.VendorCode = input.VendorCode;
                pr.VendorName = input.VendorName;
                pr.ValidFrom = input.ValidFrom;
                pr.ValidUntil = input.ValidUntil;
                pr.Remark = input.Remark;

                if (isSubmit)
                {
                    pr.Status = (int)RequestStatus.Pending;

                    var step1 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 1);
                    if (step1 != null)
                    {
                        step1.Status = (int)RequestStatus.Approved;
                        step1.ActionDate = DateTime.Now;
                        step1.ApproverNId = _currentUserService.UserId;
                        step1.ApproverName = await GetApproverNameAsync(1, _currentUserService.UserId);
                        step1.Comment = input.Comment;
                    }

                    var step2 = pr.ApprovalSteps.FirstOrDefault(s => s.Sequence == 2);
                    if (step2 != null)
                    {
                        step2.Status = (int)RequestStatus.Pending;
                        step2.ApproverNId = null;
                        step2.ApproverName = null;
                        pr.CurrentStepId = 2;
                    }
                }

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

                if (!string.IsNullOrEmpty(input.DeletedFileIds))
                {
                    var ids = input.DeletedFileIds.Split(',').Select(int.Parse).ToList();
                    var toRemove = pr.Quotations.Where(q => ids.Contains(q.Id)).ToList();
                    _context.Quotations.RemoveRange(toRemove);
                }

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
            var pr = await _context.Requests.FindAsync(id);
            if (pr != null)
            {
                _context.Requests.Remove(pr);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<AttachmentResultDto?> GetAttachmentAsync(int fileId)
        {
            var q = await _context.Quotations.Include(x => x.AttachmentFile).FirstOrDefaultAsync(x => x.Id == fileId);
            if (q == null) return null;

            if (q.AttachmentFile?.Data != null)
            {
                return new AttachmentResultDto
                {
                    Data = q.AttachmentFile.Data,
                    ContentType = q.AttachmentFile.ContentType ?? "application/octet-stream",
                    FileName = q.FileName
                };
            }

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

        private async Task<string> GenerateDocNoAsync()
        {
            var todayStr = DateTime.Now.ToString("yyyyMMdd");
            var prefix = $"QC-{todayStr}-";
            var countToday = await _context.Requests.CountAsync(x => x.Code.StartsWith(prefix));
            return $"{prefix}{(countToday + 1):D3}";
        }

        private async Task<string> GetApproverNameAsync(int routeId, string nId)
        {
            var name = await _workflowService.GetEmployeeNameFromWorkflowAsync(routeId, nId);
            return !string.IsNullOrEmpty(name) ? name : nId;
        }

        private async Task HandleFileUploadsAsync(dynamic input, Request pr)
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