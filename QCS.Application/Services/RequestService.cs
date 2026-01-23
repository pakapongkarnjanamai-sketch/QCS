using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QCS.Application.Hubs;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;
using System.Text.Json;

namespace QCS.Application.Services
{
    public interface IRequestService
    {
        IQueryable<RequestGridDto> GetMyRequestsQuery();
        Task<IQueryable<RequestGridDto>> GetMyTasksQueryAsync();
        IQueryable<RequestGridDto> GetMyApprovedListQuery();
        IQueryable<RequestGridDto> GetApprovedListQuery();
        IQueryable<RequestGridDto> GetRejectedRequestsQuery();
        Task<RequestDetailDto?> GetByCodeAsync(string code);
        Task<RequestDetailDto?> GetByIdAsync(int id);
        Task<Request> CreateAsync(CreateRequestDto input, bool isSubmit);
        Task UpdateAsync(UpdateRequestDto input, bool isSubmit);
        Task DeleteAsync(int id);
        Task<AttachmentResultDto?> GetAttachmentAsync(int id);

        Task ApproveAsync(ApprovalActionDto input);
        Task RejectAsync(ApprovalActionDto input);
    }

    public class RequestService : IRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly WorkflowService _workflowService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IWebHostEnvironment _env;
        private readonly IFileService _fileService;
        private readonly IHubContext<NotificationHub> _hubContext;

        private const int MainWorkflowId = 1;
        private const int CompletedStepId = 99;
        private const int RejectedStepId = -1;
        private const string SignalREventName = "ReceiveUpdate";

        public RequestService(
            IUnitOfWork unitOfWork,
            WorkflowService workflowService,
            ICurrentUserService currentUserService,
            IWebHostEnvironment env,
            IFileService fileService,
            IHubContext<NotificationHub> hubContext)
        {
            _unitOfWork = unitOfWork;
            _workflowService = workflowService;
            _currentUserService = currentUserService;
            _env = env;
            _fileService = fileService;
            _hubContext = hubContext;
        }

        // =================================================================================================
        // Action Methods
        // =================================================================================================

        public async Task<Request> CreateAsync(CreateRequestDto input, bool isSubmit)
        {
            using var transaction = _unitOfWork.BeginTransaction();
            try
            {
                var requestRepo = _unitOfWork.Repository<Request>();

                // Pass current user as creator for route resolution
                var routeData = await _workflowService.GetWorkflowRouteDetailAsync(MainWorkflowId, _currentUserService.UserId);

                if (routeData?.Steps == null) throw new Exception("Workflow definition not found");

                var sortedSteps = routeData.Steps.OrderBy(s => s.SequenceNo).ToList();
                var newDocNo = await GenerateDocNoAsync();

                int currentStepId = 1;
                int docStatus = isSubmit ? (int)RequestStatus.Pending : (int)RequestStatus.Draft;

                if (isSubmit)
                {
                    var nextStep = sortedSteps.FirstOrDefault(s => s.SequenceNo > 1);
                    currentStepId = nextStep != null ? nextStep.SequenceNo : CompletedStepId;
                    if (currentStepId == CompletedStepId) docStatus = (int)RequestStatus.Approved;
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
                    var actionDate = (step.SequenceNo == 1 && isSubmit) ? (DateTime?)DateTime.Now : null;
                    var stepStatus = step.SequenceNo switch
                    {
                        1 => isSubmit ? (int)RequestStatus.Approved : (int)RequestStatus.Pending,
                        2 => isSubmit ? (int)RequestStatus.Pending : (int)RequestStatus.Draft,
                        _ => (int)RequestStatus.Draft
                    };

                    pr.ApprovalSteps.Add(new ApprovalStep
                    {
                        Sequence = step.SequenceNo,
                        StepName = step.StepName,
                        Status = stepStatus,
                        ActionDate = actionDate,
                        ApproverNId = (step.SequenceNo == 1 && isSubmit) ? _currentUserService.UserId : null,
                        ApproverName = (step.SequenceNo == 1 && isSubmit) ? await GetApproverNameAsync(MainWorkflowId, _currentUserService.UserId, _currentUserService.UserId) : null,
                        Comment = (step.SequenceNo == 1 && isSubmit) ? input.Comment : null
                    });
                }

                var files = GetFilesFromInput(input);
                if (files != null && files.Any())
                {
                    var newQuotations = await _fileService.PrepareFilesForUploadAsync(files, input.QuotationsJson);
                    foreach (var q in newQuotations)
                    {
                        pr.Quotations.Add(q);
                    }
                }

                await requestRepo.AddAsync(pr);
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                await NotifyUpdatesAsync($"สร้างเอกสารใหม่ {pr.Code}");

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
            using var transaction = _unitOfWork.BeginTransaction();
            try
            {
                var requestRepo = _unitOfWork.Repository<Request>();
                var quotationRepo = _unitOfWork.Repository<Quotation>();

                var pr = await requestRepo.GetAll()
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
                        step1.ApproverName = await GetApproverNameAsync(MainWorkflowId, _currentUserService.UserId, pr.CreatedBy);
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
                    updates?.ForEach(item =>
                    {
                        var f = pr.Quotations.FirstOrDefault(q => q.Id == item.Id);
                        if (f != null) f.DocumentTypeId = item.DocumentTypeId;
                    });
                }

                if (!string.IsNullOrEmpty(input.DeletedFileIds))
                {
                    var ids = input.DeletedFileIds.Split(',').Select(int.Parse).ToList();
                    var toRemove = pr.Quotations.Where(q => ids.Contains(q.Id)).ToList();
                    await quotationRepo.DeleteRangeAsync(toRemove);
                }

                var files = GetFilesFromInput(input);
                if (files != null && files.Any())
                {
                    var newQuotations = await _fileService.PrepareFilesForUploadAsync(files, input.QuotationsJson);
                    foreach (var q in newQuotations)
                    {
                        pr.Quotations.Add(q);
                    }
                }

                await requestRepo.UpdateAsync(pr);
                await _unitOfWork.CommitAsync();
                await transaction.CommitAsync();
                await NotifyUpdatesAsync($"แก้ไขเอกสาร {pr.Code}");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task ApproveAsync(ApprovalActionDto input)
        {
            var (request, currentStepObj) = await GetRequestAndCurrentStepAsync(input.RequestId);

            var currentUserId = _currentUserService.UserId;
            string approverName = await GetApproverNameAsync(MainWorkflowId, currentUserId, request.CreatedBy);

            currentStepObj.Status = (int)RequestStatus.Approved;
            currentStepObj.ActionDate = DateTime.Now;
            currentStepObj.Comment = input.Comment;
            currentStepObj.ApproverNId = currentUserId;
            currentStepObj.ApproverName = approverName;

            var nextStep = request.ApprovalSteps
                .Where(s => s.Sequence > currentStepObj.Sequence)
                .OrderBy(s => s.Sequence)
                .FirstOrDefault();

            if (nextStep != null)
            {
                request.CurrentStepId = nextStep.Sequence;
                nextStep.Status = (int)RequestStatus.Pending;
            }
            else
            {
                request.Status = (int)RequestStatus.Approved;
                request.CurrentStepId = CompletedStepId;
            }

            await _unitOfWork.Repository<Request>().UpdateAsync(request);
            await _unitOfWork.CommitAsync();
            await NotifyUpdatesAsync($"อนุมัติเอกสาร {request.Code}");
        }

        public async Task RejectAsync(ApprovalActionDto input)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.ApprovalSteps)
                .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                .FirstOrDefaultAsync(r => r.Id == input.RequestId);

            if (request == null) throw new KeyNotFoundException("ไม่พบเอกสาร Purchase Request");

            var currentStepObj = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == request.CurrentStepId);
            if (currentStepObj == null) throw new Exception("ไม่พบข้อมูล Step ปัจจุบันของเอกสาร");

            var currentUserId = _currentUserService.UserId;
            string approverName = await GetApproverNameAsync(MainWorkflowId, currentUserId, request.CreatedBy);

            currentStepObj.Status = (int)RequestStatus.Rejected;
            currentStepObj.ActionDate = DateTime.Now;
            currentStepObj.Comment = input.Comment;
            currentStepObj.ApproverNId = currentUserId;
            currentStepObj.ApproverName = approverName;

            request.Status = (int)RequestStatus.Rejected;
            request.CurrentStepId = RejectedStepId;
            request.IsActive = false;

            if (request.ApprovalSteps != null)
                foreach (var step in request.ApprovalSteps) step.IsActive = false;

            if (request.Quotations != null)
            {
                foreach (var quotation in request.Quotations)
                {
                    quotation.IsActive = false;
                    if (quotation.AttachmentFile != null) quotation.AttachmentFile.IsActive = false;
                }
            }

            await _unitOfWork.Repository<Request>().UpdateAsync(request);
            await _unitOfWork.CommitAsync();
            await NotifyUpdatesAsync($"ไม่อนุมัติเอกสาร {request.Code}");
        }

        public async Task DeleteAsync(int id)
        {
            using var transaction = _unitOfWork.BeginTransaction();
            try
            {
                var request = await _unitOfWork.Repository<Request>().GetAll()
                    .Include(r => r.ApprovalSteps)
                    .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request != null)
                {
                    var code = request.Code;
                    var attachmentFilesToDelete = request.Quotations.Where(q => q.AttachmentFile != null).Select(q => q.AttachmentFile).ToList();
                    if (attachmentFilesToDelete.Any()) await _unitOfWork.Repository<AttachmentFile>().DeleteRangeAsync(attachmentFilesToDelete);
                    if (request.Quotations.Any()) await _unitOfWork.Repository<Quotation>().DeleteRangeAsync(request.Quotations);
                    if (request.ApprovalSteps.Any()) await _unitOfWork.Repository<ApprovalStep>().DeleteRangeAsync(request.ApprovalSteps);
                    await _unitOfWork.Repository<Request>().DeleteAsync(request);

                    await _unitOfWork.CommitAsync();
                    await transaction.CommitAsync();
                    await NotifyUpdatesAsync($"ลบเอกสาร {code}");
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =================================================================================================
        // Private Helpers
        // =================================================================================================

        private async Task NotifyUpdatesAsync(string message)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync(SignalREventName, message);
            }
            catch { }
        }

        private async Task<(Request, ApprovalStep)> GetRequestAndCurrentStepAsync(int requestId)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null) throw new KeyNotFoundException("ไม่พบเอกสาร Purchase Request");

            var currentStepObj = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == request.CurrentStepId);
            if (currentStepObj == null) throw new Exception("ไม่พบข้อมูล Step ปัจจุบันของเอกสาร");

            return (request, currentStepObj);
        }

        private async Task<string> GenerateDocNoAsync()
        {
            var todayStr = DateTime.Now.ToString("yyyyMMdd");
            var prefix = $"QC-{todayStr}-";
            var countToday = await _unitOfWork.Repository<Request>().GetAll().CountAsync(x => x.Code.StartsWith(prefix));
            return $"{prefix}{(countToday + 1):D3}";
        }

        private async Task<string> GetApproverNameAsync(int routeId, string nId, string? createdBy)
        {
            // ส่ง createdBy ของเอกสารไปด้วย เพื่อให้ Workflow Service Resolve ชื่อได้ถูกต้อง
            var name = await _workflowService.GetEmployeeNameFromWorkflowAsync(routeId, nId, createdBy);
            return !string.IsNullOrEmpty(name) ? name : nId;
        }

        private List<IFormFile> GetFilesFromInput(object input)
        {
            if (input is IHasAttachments dto)
            {
                return dto.GetUploadFiles() ?? new List<IFormFile>();
            }
            return new List<IFormFile>();
        }

        // =================================================================================================
        // Query Methods
        // =================================================================================================

        public IQueryable<RequestGridDto> GetMyRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
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
            return _unitOfWork.Repository<Request>().GetAll()
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
            return _unitOfWork.Repository<Request>().GetAll()
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
                    RequesterName = r.ApprovalSteps.Where(s => s.Sequence == 1).Select(s => s.ApproverName).FirstOrDefault() ?? "Unknown",
                    ValidFrom = r.ValidFrom,
                    ValidUntil = r.ValidUntil
                });
        }

        public async Task<IQueryable<RequestGridDto>> GetMyTasksQueryAsync()
        {
            // ปรับปรุง: ตรวจสอบ Task จากฐานข้อมูล (ApprovalSteps) โดยตรงแทนการเรียก Workflow API
            // เนื่องจาก Workflow ขึ้นอยู่กับ Creator ทำให้ไม่สามารถใช้ Logic เดิม (Get Route 1 ครั้งแล้ว Filter) ได้ง่ายๆ
            // การเช็คจาก DB จะแม่นยำกว่า เพราะ Step ถูก Generate และ Resolve Assignee ลง DB แล้วตอน Create/Update

            var currentUserId = _currentUserService.UserId;

            // คืนค่า Queryable ที่ Filter จาก DB โดยตรง
            var query = _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Pending &&
                            r.ApprovalSteps.Any(s => s.Sequence == r.CurrentStepId && s.ApproverNId == currentUserId))
                .Select(r => new RequestGridDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId,
                    RequesterName = r.ApprovalSteps.Where(s => s.Sequence == 1).Select(s => s.ApproverName).FirstOrDefault() ?? "Unknown"
                });

            return await Task.FromResult(query);
        }

        public IQueryable<RequestGridDto> GetMyApprovedListQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.CreatedBy == _currentUserService.UserId && r.Status == (int)RequestStatus.Approved)
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

        public async Task<RequestDetailDto?> GetByIdAsync(int id)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.Quotations)
                .Include(r => r.ApprovalSteps)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return null;

            bool isFinalState = request.Status == (int)RequestStatus.Approved ||
                                request.Status == (int)RequestStatus.Rejected;

            WorkflowRouteDetailDto? workflowRoute = null;
            PermissionDto permissions = new PermissionDto();

            if (isFinalState)
            {
                workflowRoute = new WorkflowRouteDetailDto
                {
                    Id = 0,
                    RouteName = "History",
                    CanInitiate = false,
                    Steps = request.ApprovalSteps.Select(step => new WorkflowStepDto
                    {
                        Id = step.Id,
                        SequenceNo = step.Sequence,
                        StepName = step.StepName,
                        Status = step.Status,
                        ActionDate = step.ActionDate,
                        ApproverName = step.ApproverName,
                        ApproverNId = step.ApproverNId,
                        Comment = step.Comment,
                        Assignments = !string.IsNullOrEmpty(step.ApproverNId)
                            ? new List<AssignmentDto> { new AssignmentDto { NId = step.ApproverNId, EmployeeName = step.ApproverName ?? "", IsCurrentUser = step.ApproverNId == _currentUserService.UserId } }
                            : new List<AssignmentDto>()
                    }).OrderBy(s => s.SequenceNo).ToList()
                };
            }
            else
            {
                // ส่ง createdBy (เจ้าของเอกสาร) ไปเพื่อให้ Workflow Service Resolve ผู้อนุมัติที่ถูกต้องสำหรับเอกสารนี้
                workflowRoute = await _workflowService.GetWorkflowRouteDetailAsync(MainWorkflowId, request.CreatedBy);

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
                permissions = _workflowService.GetPermissions(request, workflowRoute);
            }

            return new RequestDetailDto
            {
                RequestId = request.Id,
                Code = request.Code,
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
                Permissions = permissions,
                WorkflowRoute = workflowRoute
            };
        }

        public async Task<RequestDetailDto?> GetByCodeAsync(string code)
        {
            var id = await _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Code == code)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            return id == 0 ? null : await GetByIdAsync(id);
        }

        public async Task<AttachmentResultDto?> GetAttachmentAsync(int fileId)
        {
            var q = await _unitOfWork.Repository<Quotation>().GetAll()
                .Include(x => x.AttachmentFile)
                .FirstOrDefaultAsync(x => x.Id == fileId);

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
            return null;
        }
    }
}