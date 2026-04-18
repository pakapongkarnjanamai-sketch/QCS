using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QCS.Application.Hubs;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;
using System.Linq.Expressions;
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
        Task<int> GetMyPendingTaskCountAsync();
    }

    public class RequestService : IRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly WorkflowService _workflowService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDateTime _dateTime;
        private readonly IFileService _fileService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<RequestService> _logger;

        private const int MainWorkflowId = 1;
        private const int CompletedStepId = 99;
        private const int RejectedStepId = -1;
        private const string SignalREventName = "ReceiveUpdate";
        private const int GenerateDocNoRetryLimit = 3;
        private const string RequestCodeUniqueIndexName = "IX_Requests_Code";

        private static readonly Expression<Func<Request, RequestGridDto>> RequestGridProjection = r => new RequestGridDto
        {
            Id = r.Id,
            Code = r.Code,
            Title = r.Title,
            VendorCode = r.VendorCode,
            VendorName = r.VendorName,
            RequestDate = r.RequestDate,
            CurrentStepId = r.CurrentStepId,
            Remark = r.Remark ?? string.Empty
        };

        private static readonly Expression<Func<Request, RequestGridDto>> RequestGridWithRequesterProjection = r => new RequestGridDto
        {
            Id = r.Id,
            Code = r.Code,
            Title = r.Title,
            VendorCode = r.VendorCode,
            VendorName = r.VendorName,
            RequestDate = r.RequestDate,
            CurrentStepId = r.CurrentStepId,
            Remark = r.Remark ?? string.Empty,
            RequesterName = r.ApprovalSteps.Where(s => s.Sequence == 1).Select(s => s.ApproverName).FirstOrDefault() ?? "Unknown",
            ValidFrom = r.ValidFrom,
            ValidUntil = r.ValidUntil
        };

        public RequestService(
            IUnitOfWork unitOfWork,
            WorkflowService workflowService,
            ICurrentUserService currentUserService,
            IDateTime dateTime,
            IFileService fileService,
            IHubContext<NotificationHub> hubContext,
            ILogger<RequestService> logger)
        {
            _unitOfWork = unitOfWork;
            _workflowService = workflowService;
            _currentUserService = currentUserService;
            _dateTime = dateTime;
            _fileService = fileService;
            _hubContext = hubContext;
            _logger = logger;
        }

        // =================================================================================================
        // Action Methods
        // =================================================================================================

        public async Task<Request> CreateAsync(CreateRequestDto input, bool isSubmit)
        {
            for (var attempt = 1; attempt <= GenerateDocNoRetryLimit; attempt++)
            {
                using var transaction = _unitOfWork.BeginTransaction();
                try
                {
                    var requestRepo = _unitOfWork.Repository<Request>();
                    var request = await BuildRequestForCreateAsync(input, isSubmit);

                    await requestRepo.AddAsync(request);
                    await _unitOfWork.CommitAsync();
                    await transaction.CommitAsync();
                    await NotifyUpdatesAsync($"สร้างเอกสารใหม่ {request.Code}");

                    return request;
                }
                catch (DbUpdateException ex) when (IsRequestCodeConflict(ex))
                {
                    await transaction.RollbackAsync();
                    _unitOfWork.ClearTrackedChanges();

                    if (attempt == GenerateDocNoRetryLimit)
                    {
                        throw new InvalidOperationException("Unable to generate a unique document number after multiple attempts.", ex);
                    }

                    _logger.LogWarning(ex, "Request code conflict on create attempt {Attempt}. Retrying with a new document number.", attempt);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            throw new InvalidOperationException("Unable to create request.");
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
                    await ApplyDraftSubmissionStateAsync(pr, input.Comment);
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
                if (files.Count > 0)
                {
                    var newQuotations = await _fileService.PrepareFilesForUploadAsync(files, input.QuotationsJson ?? string.Empty);
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
            catch (Exception)
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
            currentStepObj.ActionDate = _dateTime.Now;
            currentStepObj.Comment = input.Comment;
            currentStepObj.ApproverNId = currentUserId;
            currentStepObj.ApproverName = approverName;

            var nextStep = GetNextApprovalStep(request.ApprovalSteps, currentStepObj.Sequence);

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

            var currentStepObj = GetCurrentStepOrThrow(request);

            var currentUserId = _currentUserService.UserId;
            string approverName = await GetApproverNameAsync(MainWorkflowId, currentUserId, request.CreatedBy);

            currentStepObj.Status = (int)RequestStatus.Rejected;
            currentStepObj.ActionDate = _dateTime.Now;
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
            catch (Exception)
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
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Notification broadcast was canceled for message: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast update notification: {Message}", message);
            }
        }

        private async Task<(Request, ApprovalStep)> GetRequestAndCurrentStepAsync(int requestId)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null) throw new KeyNotFoundException("ไม่พบเอกสาร Purchase Request");

            return (request, GetCurrentStepOrThrow(request));
        }

        private async Task<Request> BuildRequestForCreateAsync(CreateRequestDto input, bool isSubmit)
        {
            var creatorUserId = _currentUserService.UserId;
            var sortedSteps = await GetSortedWorkflowStepsAsync(creatorUserId);
            var newDocNo = await GenerateDocNoAsync();
            var (docStatus, currentStepId) = isSubmit
                ? GetSubmittedDocumentState(sortedSteps)
                : ((int)RequestStatus.Draft, 1);

            var request = new Request
            {
                Code = newDocNo,
                Title = input.Title,
                RequestDate = _dateTime.Now,
                Status = docStatus,
                CurrentStepId = currentStepId,
                VendorCode = input.VendorCode,
                VendorName = input.VendorName,
                ValidFrom = input.ValidFrom,
                ValidUntil = input.ValidUntil,
                Remark = input.Remark
            };

            foreach (var step in await BuildApprovalStepsAsync(sortedSteps, isSubmit, input.Comment, creatorUserId))
            {
                request.ApprovalSteps.Add(step);
            }

            var files = GetFilesFromInput(input);
            if (files.Count > 0)
            {
                var newQuotations = await _fileService.PrepareFilesForUploadAsync(files, input.QuotationsJson ?? string.Empty);
                foreach (var quotation in newQuotations)
                {
                    request.Quotations.Add(quotation);
                }
            }

            return request;
        }

        private async Task<List<WorkflowStepDto>> GetSortedWorkflowStepsAsync(string creatorUserId)
        {
            var routeData = await _workflowService.GetWorkflowRouteDetailAsync(MainWorkflowId, creatorUserId);
            if (routeData?.Steps == null)
            {
                throw new InvalidOperationException("Workflow definition not found");
            }

            return routeData.Steps.OrderBy(s => s.SequenceNo).ToList();
        }

        private static (int status, int currentStepId) GetSubmittedDocumentState(IReadOnlyList<WorkflowStepDto> sortedSteps)
        {
            var nextStep = sortedSteps.FirstOrDefault(s => s.SequenceNo > 1);
            if (nextStep == null)
            {
                return ((int)RequestStatus.Approved, CompletedStepId);
            }

            return ((int)RequestStatus.Pending, nextStep.SequenceNo);
        }

        private async Task<List<ApprovalStep>> BuildApprovalStepsAsync(
            IReadOnlyList<WorkflowStepDto> sortedSteps,
            bool isSubmit,
            string? submitComment,
            string creatorUserId)
        {
            string? submitterName = null;
            if (isSubmit)
            {
                submitterName = await GetApproverNameAsync(MainWorkflowId, _currentUserService.UserId, creatorUserId);
            }

            return sortedSteps.Select(step => new ApprovalStep
            {
                Sequence = step.SequenceNo,
                StepName = step.StepName,
                Status = step.SequenceNo switch
                {
                    1 => isSubmit ? (int)RequestStatus.Approved : (int)RequestStatus.Draft,
                    2 => isSubmit ? (int)RequestStatus.Pending : (int)RequestStatus.Draft,
                    _ => (int)RequestStatus.Draft
                },
                ActionDate = step.SequenceNo == 1 && isSubmit ? _dateTime.Now : null,
                ApproverNId = step.SequenceNo == 1 && isSubmit ? _currentUserService.UserId : null,
                ApproverName = step.SequenceNo == 1 && isSubmit ? submitterName : null,
                Comment = step.SequenceNo == 1 && isSubmit ? submitComment : null
            }).ToList();
        }

        private async Task ApplyDraftSubmissionStateAsync(Request request, string? submitComment)
        {
            request.Status = (int)RequestStatus.Pending;

            var step1 = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == 1);
            if (step1 != null)
            {
                step1.Status = (int)RequestStatus.Approved;
                step1.ActionDate = _dateTime.Now;
                step1.ApproverNId = _currentUserService.UserId;
                step1.ApproverName = await GetApproverNameAsync(MainWorkflowId, _currentUserService.UserId, request.CreatedBy);
                step1.Comment = submitComment;
            }

            var nextStep = GetNextApprovalStep(request.ApprovalSteps, 1);
            if (nextStep == null)
            {
                request.Status = (int)RequestStatus.Approved;
                request.CurrentStepId = CompletedStepId;
                return;
            }

            nextStep.Status = (int)RequestStatus.Pending;
            nextStep.ApproverNId = null;
            nextStep.ApproverName = null;
            request.CurrentStepId = nextStep.Sequence;
        }

        private static ApprovalStep? GetNextApprovalStep(IEnumerable<ApprovalStep> steps, int currentSequence)
        {
            return steps
                .Where(s => s.Sequence > currentSequence)
                .OrderBy(s => s.Sequence)
                .FirstOrDefault();
        }

        private static ApprovalStep GetCurrentStepOrThrow(Request request)
        {
            var currentStep = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == request.CurrentStepId);
            if (currentStep == null)
            {
                throw new InvalidOperationException("ไม่พบข้อมูล Step ปัจจุบันของเอกสาร");
            }

            return currentStep;
        }

        private async Task<string> GenerateDocNoAsync()
        {
            var todayStr = _dateTime.Now.ToString("yyyyMMdd");
            var prefix = $"QC-{todayStr}-";
            var countToday = await _unitOfWork.Repository<Request>().GetAll().CountAsync(x => x.Code.StartsWith(prefix));
            return $"{prefix}{(countToday + 1):D3}";
        }

        private static bool IsRequestCodeConflict(DbUpdateException exception)
        {
            return exception.InnerException?.Message.Contains(RequestCodeUniqueIndexName, StringComparison.OrdinalIgnoreCase) == true
                || exception.Message.Contains(RequestCodeUniqueIndexName, StringComparison.OrdinalIgnoreCase);
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

        private IQueryable<Request> GetActiveRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll().Where(r => r.IsActive);
        }

        private static bool IsCurrentUserAssignedToStep(WorkflowStepDto? step, string currentUserId)
        {
            return step?.Assignments?.Any(a => string.Equals(a.NId, currentUserId, StringComparison.OrdinalIgnoreCase)) ?? false;
        }

        private sealed class MyPendingTaskResult
        {
            public List<int> TaskIds { get; } = new List<int>();
            public int TotalCount { get; set; }
        }

        private async Task<MyPendingTaskResult> GetMyPendingTaskResultAsync(string currentUserId)
        {
            var pendingCandidates = await GetActiveRequestsQuery()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Pending && !string.IsNullOrEmpty(r.CreatedBy))
                .Select(r => new { r.Id, r.CreatedBy, r.CurrentStepId })
                .ToListAsync();

            var groupedCandidates = pendingCandidates
                .GroupBy(x => new { x.CreatedBy, x.CurrentStepId })
                .Select(g => new
                {
                    CreatedBy = g.Key.CreatedBy,
                    CurrentStepId = g.Key.CurrentStepId,
                    Count = g.Count(),
                    TaskIds = g.Select(x => x.Id).ToList()
                })
                .ToList();

            var result = new MyPendingTaskResult();

            foreach (var group in groupedCandidates)
            {
                if (string.IsNullOrEmpty(group.CreatedBy))
                {
                    continue;
                }

                var route = await _workflowService.GetWorkflowRouteDetailAsync(MainWorkflowId, group.CreatedBy);
                var stepConfig = route?.Steps?.FirstOrDefault(s => s.SequenceNo == group.CurrentStepId);

                if (!IsCurrentUserAssignedToStep(stepConfig, currentUserId))
                {
                    continue;
                }

                result.TotalCount += group.Count;
                result.TaskIds.AddRange(group.TaskIds);
            }

            return result;
        }

        // =================================================================================================
        // Query Methods
        // =================================================================================================

        public IQueryable<RequestGridDto> GetMyRequestsQuery()
        {
            return GetActiveRequestsQuery()
                .AsNoTracking()
                .Where(r => r.CreatedBy == _currentUserService.UserId
                         && r.Status != (int)RequestStatus.Approved
                         && r.Status != (int)RequestStatus.Rejected)
                .Select(RequestGridProjection);
        }

        public IQueryable<RequestGridDto> GetRejectedRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.CreatedBy == _currentUserService.UserId && r.Status == (int)RequestStatus.Rejected)
                .Select(RequestGridProjection);
        }

        public IQueryable<RequestGridDto> GetApprovedListQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Approved)
                .Select(RequestGridWithRequesterProjection);
        }

        public async Task<IQueryable<RequestGridDto>> GetMyTasksQueryAsync()
        {
            var currentUserId = _currentUserService.UserId;

            var taskResult = await GetMyPendingTaskResultAsync(currentUserId);
            if (taskResult.TaskIds.Count == 0)
            {
                return GetActiveRequestsQuery()
                    .AsNoTracking()
                    .Where(r => false)
                    .Select(RequestGridWithRequesterProjection);
            }

            return GetActiveRequestsQuery()
                .AsNoTracking()
                .Where(r => taskResult.TaskIds.Contains(r.Id))
                .Select(RequestGridWithRequesterProjection);
        }

        public IQueryable<RequestGridDto> GetMyApprovedListQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.CreatedBy == _currentUserService.UserId && r.Status == (int)RequestStatus.Approved)
                .Select(RequestGridProjection);
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
                            ? new List<AssignmentDto> { new AssignmentDto { NId = step.ApproverNId, EmployeeName = step.ApproverName ?? "", IsCurrentUser = string.Equals(step.ApproverNId, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase) } }
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

            workflowRoute ??= new WorkflowRouteDetailDto
            {
                Id = 0,
                RouteName = string.Empty,
                CanInitiate = false,
                Steps = new List<WorkflowStepDto>()
            };

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
                }).OrderBy(d => d.DocumentTypeId).ToList(),
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

        public async Task<int> GetMyPendingTaskCountAsync()
        {
            var currentUserId = _currentUserService.UserId;
            var taskResult = await GetMyPendingTaskResultAsync(currentUserId);
            return taskResult.TotalCount;
        }
    }
}