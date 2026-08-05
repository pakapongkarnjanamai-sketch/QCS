using Microsoft.AspNetCore.Http;
using QCS.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QCS.Application.Hubs;
using QCS.Domain.DTOs;
using QCS.Domain.DTOs.Portal;
using QCS.Domain.Enum;
using QCS.Domain.Models;
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
        IQueryable<SourcedRequestDto> GetBySourceQuery(string system, string number);

        // Admin views — no current-user filter
        IQueryable<RequestGridDto> GetAllRequestsQuery();
        IQueryable<RequestGridDto> GetAllDraftRequestsQuery();
        IQueryable<RequestGridDto> GetAllPendingRequestsQuery();
        IQueryable<RequestGridDto> GetAllApprovedRequestsQuery();
        IQueryable<RequestGridDto> GetAllRejectedRequestsQuery();
        Task<RequestDetailDto?> GetByCodeAsync(string code);
        Task<RequestDetailDto?> GetByIdAsync(int id);
        Task<Request> CreateAsync(CreateRequestDto input, bool isSubmit);
        Task UpdateAsync(UpdateRequestDto input, bool isSubmit);
        Task DeleteAsync(int id);
        Task<AttachmentResultDto?> GetAttachmentAsync(int id);

        Task ApproveAsync(ApprovalActionDto input);
        Task RejectAsync(ApprovalActionDto input);
        Task<int> GetMyPendingTaskCountAsync();
        Task<PortalPage<PortalRequestListItemDto>> GetPortalRequestsAsync(PortalRequestQuery query, CancellationToken cancellationToken = default);
        Task<PortalRequestDetailDto?> GetPortalRequestByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<PortalRequestDetailDto?> GetPortalRequestByCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<PortalSaveResultDto> CreatePortalDraftAsync(SavePortalRequestDto input, CancellationToken cancellationToken = default);
        Task<PortalSaveResultDto> UpdatePortalDraftAsync(int id, SavePortalRequestDto input, CancellationToken cancellationToken = default);
        Task SubmitPortalRequestAsync(int id, CancellationToken cancellationToken = default);
        Task DeletePortalDraftAsync(int id, CancellationToken cancellationToken = default);
        Task<PortalAttachmentDto> AddPortalAttachmentAsync(int requestId, UploadPortalAttachmentDto input, CancellationToken cancellationToken = default);
        Task DeletePortalAttachmentAsync(int requestId, int attachmentId, CancellationToken cancellationToken = default);
        Task ApprovePortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default);
        Task RejectPortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default);
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
            RequesterNId = r.CreatedBy ?? string.Empty,
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
                pr.SourceSystem = input.SourceSystem;
                pr.SourceCode = input.SourceCode;
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

                if (request == null)
                    throw new KeyNotFoundException($"Request {id} not found.");

                if (request.Status != (int)RequestStatus.Draft)
                    throw new InvalidOperationException("Only draft requests can be deleted.");

                if (!string.Equals(request.CreatedBy, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("You can only delete your own draft requests.");

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
                SourceSystem = input.SourceSystem,
                SourceCode = input.SourceCode,
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
            if (routeData?.Steps == null || routeData.Steps.Count == 0)
            {
                return new List<WorkflowStepDto>
                {
                    new WorkflowStepDto { Id = 1, SequenceNo = 1, StepName = "Submitter" },
                    new WorkflowStepDto { Id = 2, SequenceNo = 2, StepName = "Manager Approval" }
                };
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

        public IQueryable<SourcedRequestDto> GetBySourceQuery(string system, string number)
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.SourceSystem == system && r.SourceCode == number)
                .Select(r => new SourcedRequestDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    Status = r.Status,
                    StatusName = r.Status == (int)RequestStatus.Draft ? nameof(RequestStatus.Draft)
                        : r.Status == (int)RequestStatus.Pending ? nameof(RequestStatus.Pending)
                        : r.Status == (int)RequestStatus.Approved ? nameof(RequestStatus.Approved)
                        : r.Status == (int)RequestStatus.Rejected ? nameof(RequestStatus.Rejected)
                        : "Unknown",
                    CurrentStepId = r.CurrentStepId,
                    CurrentStepName = r.ApprovalSteps
                        .Where(s => s.Sequence == r.CurrentStepId)
                        .Select(s => s.StepName)
                        .FirstOrDefault(),
                    RequesterNId = r.CreatedBy ?? string.Empty,
                    RequesterName = r.ApprovalSteps
                        .Where(s => s.Sequence == 1)
                        .Select(s => s.ApproverName)
                        .FirstOrDefault() ?? "Unknown",
                    ValidFrom = r.ValidFrom,
                    ValidUntil = r.ValidUntil,
                    Remark = r.Remark,
                    Documents = r.Quotations
                        .OrderBy(q => q.DocumentTypeId)
                        .Select(q => new SourcedDocumentDto
                        {
                            Id = q.Id,
                            FileName = q.FileName,
                            DocumentTypeId = q.DocumentTypeId,
                            DocumentTypeName = q.DocumentTypeId == (int)DocumentType.OriginalQuotation ? nameof(DocumentType.OriginalQuotation)
                                : q.DocumentTypeId == (int)DocumentType.Comparison ? nameof(DocumentType.Comparison)
                                : q.DocumentTypeId == (int)DocumentType.Specifications ? nameof(DocumentType.Specifications)
                                : q.DocumentTypeId == (int)DocumentType.Attachment ? nameof(DocumentType.Attachment)
                                : q.DocumentTypeId == (int)DocumentType.ExpiredQuotation ? nameof(DocumentType.ExpiredQuotation)
                                : "Unknown"
                        })
                        .ToList()
                });
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

        public IQueryable<RequestGridDto> GetAllRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Select(RequestGridWithRequesterProjection);
        }

        public IQueryable<RequestGridDto> GetAllDraftRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Draft)
                .Select(RequestGridWithRequesterProjection);
        }

        public IQueryable<RequestGridDto> GetAllPendingRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Pending)
                .Select(RequestGridWithRequesterProjection);
        }

        public IQueryable<RequestGridDto> GetAllApprovedRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Approved)
                .Select(RequestGridWithRequesterProjection);
        }

        public IQueryable<RequestGridDto> GetAllRejectedRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Rejected)
                .Select(RequestGridWithRequesterProjection);
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
                RequesterName = request.ApprovalSteps
                    .Where(s => s.Sequence == 1)
                    .Select(s => s.ApproverName)
                    .FirstOrDefault() ?? string.Empty,
                CurrentStepId = request.CurrentStepId,
                VendorCode = request.VendorCode,
                VendorName = request.VendorName,
                SourceSystem = request.SourceSystem,
                SourceCode = request.SourceCode,
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

        public async Task<PortalPage<PortalRequestListItemDto>> GetPortalRequestsAsync(
            PortalRequestQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (string.IsNullOrWhiteSpace(query.View) ||
                !Enum.TryParse<PortalRequestView>(query.View, ignoreCase: true, out var view))
            {
                throw new ArgumentException($"Invalid view '{query.View}'. Valid values are: {nameof(PortalRequestView.MyTasks)}, {nameof(PortalRequestView.MyRequests)}, {nameof(PortalRequestView.MyApproved)}, {nameof(PortalRequestView.Rejected)}, {nameof(PortalRequestView.AllApproved)}.");
            }

            var currentUserId = _currentUserService.UserId;

            IQueryable<Request> requestsQuery;

            switch (view)
            {
                case PortalRequestView.MyTasks:
                    var taskResult = await GetMyPendingTaskResultAsync(currentUserId);
                    if (taskResult.TaskIds.Count == 0)
                    {
                        requestsQuery = GetActiveRequestsQuery().AsNoTracking().Where(r => false);
                    }
                    else
                    {
                        requestsQuery = GetActiveRequestsQuery().AsNoTracking().Where(r => taskResult.TaskIds.Contains(r.Id));
                    }
                    break;

                case PortalRequestView.MyRequests:
                    requestsQuery = GetActiveRequestsQuery()
                        .AsNoTracking()
                        .Where(r => r.CreatedBy == currentUserId
                                 && r.Status != (int)RequestStatus.Approved
                                 && r.Status != (int)RequestStatus.Rejected);
                    break;

                case PortalRequestView.MyApproved:
                    requestsQuery = _unitOfWork.Repository<Request>().GetAll()
                        .AsNoTracking()
                        .Where(r => r.CreatedBy == currentUserId && r.Status == (int)RequestStatus.Approved);
                    break;

                case PortalRequestView.Rejected:
                    requestsQuery = _unitOfWork.Repository<Request>().GetAll()
                        .AsNoTracking()
                        .Where(r => r.CreatedBy == currentUserId && r.Status == (int)RequestStatus.Rejected);
                    break;

                case PortalRequestView.AllApproved:
                    requestsQuery = _unitOfWork.Repository<Request>().GetAll()
                        .AsNoTracking()
                        .Where(r => r.Status == (int)RequestStatus.Approved);
                    break;

                default:
                    throw new ArgumentException($"Unsupported view '{query.View}'.");
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                requestsQuery = requestsQuery.Where(r =>
                    r.Code.Contains(s) ||
                    r.Title.Contains(s) ||
                    r.VendorCode.Contains(s) ||
                    r.VendorName.Contains(s) ||
                    (r.Remark != null && r.Remark.Contains(s)));
            }

            bool isDescending = query.SortDescending;
            string sortBy = (query.SortBy ?? string.Empty).Trim().ToLowerInvariant();

            requestsQuery = sortBy switch
            {
                "code" => isDescending ? requestsQuery.OrderByDescending(r => r.Code) : requestsQuery.OrderBy(r => r.Code),
                "title" => isDescending ? requestsQuery.OrderByDescending(r => r.Title) : requestsQuery.OrderBy(r => r.Title),
                "vendorcode" => isDescending ? requestsQuery.OrderByDescending(r => r.VendorCode) : requestsQuery.OrderBy(r => r.VendorCode),
                "vendorname" => isDescending ? requestsQuery.OrderByDescending(r => r.VendorName) : requestsQuery.OrderBy(r => r.VendorName),
                "status" => isDescending ? requestsQuery.OrderByDescending(r => r.Status) : requestsQuery.OrderBy(r => r.Status),
                "requestdate" or "date" => isDescending ? requestsQuery.OrderByDescending(r => r.RequestDate) : requestsQuery.OrderBy(r => r.RequestDate),
                _ => isDescending ? requestsQuery.OrderByDescending(r => r.Id) : requestsQuery.OrderByDescending(r => r.RequestDate).ThenByDescending(r => r.Id)
            };

            int page = query.Page;
            int pageSize = query.PageSize;

            int totalCount = await requestsQuery.CountAsync(cancellationToken);

            var items = await requestsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new PortalRequestListItemDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    RequestDate = r.RequestDate,
                    CurrentStepId = r.CurrentStepId,
                    Status = r.Status,
                    StatusName = r.Status == (int)RequestStatus.Draft ? nameof(RequestStatus.Draft)
                        : r.Status == (int)RequestStatus.Pending ? nameof(RequestStatus.Pending)
                        : r.Status == (int)RequestStatus.Approved ? nameof(RequestStatus.Approved)
                        : r.Status == (int)RequestStatus.Rejected ? nameof(RequestStatus.Rejected)
                        : "Unknown",
                    // Matches the detail path: the sequence-1 approver name is only filled on
                    // submit, so a draft would otherwise read "Unknown" to the user who created it.
                    RequesterName = r.ApprovalSteps
                        .Where(s => s.Sequence == 1 && s.ApproverName != null && s.ApproverName != "")
                        .Select(s => s.ApproverName)
                        .FirstOrDefault() ?? (r.CreatedBy ?? string.Empty),
                    RequesterNId = r.CreatedBy ?? string.Empty,
                    Remark = r.Remark ?? string.Empty,
                    ValidFrom = r.ValidFrom,
                    ValidUntil = r.ValidUntil
                })
                .ToListAsync(cancellationToken);

            return new PortalPage<PortalRequestListItemDto>(items, totalCount, page, pageSize);
        }

        public async Task<PortalRequestDetailDto?> GetPortalRequestByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.Quotations)
                .Include(r => r.ApprovalSteps)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null) return null;

            var currentUserId = _currentUserService.UserId;

            bool isCreator = string.Equals(request.CreatedBy, currentUserId, StringComparison.OrdinalIgnoreCase);
            bool isApproved = request.Status == (int)RequestStatus.Approved;
            bool isAssigned = false;

            if (!isCreator && !isApproved)
            {
                var taskResult = await GetMyPendingTaskResultAsync(currentUserId);
                if (taskResult.TaskIds.Contains(request.Id))
                {
                    isAssigned = true;
                }
                else if (request.ApprovalSteps.Any(s => string.Equals(s.ApproverNId, currentUserId, StringComparison.OrdinalIgnoreCase)))
                {
                    isAssigned = true;
                }
            }

            if (!isCreator && !isApproved && !isAssigned)
            {
                throw new UnauthorizedAccessException("You do not have permission to view this request.");
            }

            bool isFinalState = isApproved || request.Status == (int)RequestStatus.Rejected;

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
                            ? new List<AssignmentDto> { new AssignmentDto { NId = step.ApproverNId, EmployeeName = step.ApproverName ?? "", IsCurrentUser = string.Equals(step.ApproverNId, currentUserId, StringComparison.OrdinalIgnoreCase) } }
                            : new List<AssignmentDto>()
                    }).OrderBy(s => s.SequenceNo).ToList()
                };
            }
            else
            {
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

            var step1 = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == 1);
            string requesterName = !string.IsNullOrWhiteSpace(step1?.ApproverName)
                ? step1.ApproverName
                : (request.CreatedBy ?? string.Empty);
            string requesterNId = request.CreatedBy ?? string.Empty;

            string statusName = request.Status == (int)RequestStatus.Draft ? nameof(RequestStatus.Draft)
                : request.Status == (int)RequestStatus.Pending ? nameof(RequestStatus.Pending)
                : request.Status == (int)RequestStatus.Approved ? nameof(RequestStatus.Approved)
                : request.Status == (int)RequestStatus.Rejected ? nameof(RequestStatus.Rejected)
                : "Unknown";

            string? currentStepName = request.ApprovalSteps
                .FirstOrDefault(s => s.Sequence == request.CurrentStepId)?.StepName;

            var portalWorkflowSteps = (workflowRoute.Steps ?? new List<WorkflowStepDto>())
                .OrderBy(s => s.SequenceNo)
                .Select(s => new PortalWorkflowStepDto
                {
                    Id = s.Id,
                    SequenceNo = s.SequenceNo,
                    StepName = s.StepName,
                    Status = s.Status,
                    StatusName = s.Status.HasValue
                        ? (s.Status == (int)RequestStatus.Draft ? nameof(RequestStatus.Draft)
                            : s.Status == (int)RequestStatus.Pending ? nameof(RequestStatus.Pending)
                            : s.Status == (int)RequestStatus.Approved ? nameof(RequestStatus.Approved)
                            : s.Status == (int)RequestStatus.Rejected ? nameof(RequestStatus.Rejected)
                            : "Unknown")
                        : null,
                    ActionDate = s.ActionDate,
                    ApproverNId = s.ApproverNId,
                    ApproverName = s.ApproverName,
                    Comment = s.Comment,
                    Assignments = (s.Assignments ?? new List<AssignmentDto>()).Select(a => new PortalAssignmentDto
                    {
                        NId = a.NId,
                        EmployeeName = a.EmployeeName,
                        AssignmentType = a.AssignmentType,
                        IsCurrentUser = string.Equals(a.NId, currentUserId, StringComparison.OrdinalIgnoreCase)
                    }).ToList()
                }).ToList();

            var documents = request.Quotations
                .OrderBy(q => q.DocumentTypeId)
                .Select(q => new PortalDocumentDto
                {
                    Id = q.Id,
                    FileName = q.FileName,
                    DocumentTypeId = q.DocumentTypeId,
                    DocumentTypeName = q.DocumentTypeId == (int)DocumentType.OriginalQuotation ? nameof(DocumentType.OriginalQuotation)
                        : q.DocumentTypeId == (int)DocumentType.Comparison ? nameof(DocumentType.Comparison)
                        : q.DocumentTypeId == (int)DocumentType.Specifications ? nameof(DocumentType.Specifications)
                        : q.DocumentTypeId == (int)DocumentType.Attachment ? nameof(DocumentType.Attachment)
                        : q.DocumentTypeId == (int)DocumentType.ExpiredQuotation ? nameof(DocumentType.ExpiredQuotation)
                        : "Unknown",
                    FileSize = q.FileSize,
                    ViewUrl = $"/api/Request/ViewFile/{q.Id}"
                }).ToList();

            if (request.Status == (int)RequestStatus.Approved)
            {
                documents.Add(new PortalDocumentDto
                {
                    Id = request.Id,
                    FileName = $"{request.Code}.pdf",
                    DocumentTypeId = 99,
                    DocumentTypeName = "FinalPdf",
                    ViewUrl = $"/api/Quotation/ViewFile/{request.Id}"
                });
            }

            var histories = request.ApprovalSteps
                .Where(s => s.ActionDate != null || s.Status == (int)RequestStatus.Approved || s.Status == (int)RequestStatus.Rejected)
                .OrderBy(s => s.Sequence)
                .Select(s => new PortalHistoryDto
                {
                    SequenceNo = s.Sequence,
                    StepName = s.StepName,
                    Status = s.Status,
                    StatusName = s.Status == (int)RequestStatus.Draft ? nameof(RequestStatus.Draft)
                        : s.Status == (int)RequestStatus.Pending ? nameof(RequestStatus.Pending)
                        : s.Status == (int)RequestStatus.Approved ? nameof(RequestStatus.Approved)
                        : s.Status == (int)RequestStatus.Rejected ? nameof(RequestStatus.Rejected)
                        : "Unknown",
                    ApproverNId = s.ApproverNId,
                    ApproverName = s.ApproverName,
                    ActionDate = s.ActionDate,
                    Comment = s.Comment
                }).ToList();

            return new PortalRequestDetailDto
            {
                Id = request.Id,
                Code = request.Code,
                Title = request.Title,
                RequestDate = request.RequestDate,
                Status = request.Status,
                StatusName = statusName,
                RequesterNId = requesterNId,
                RequesterName = requesterName,
                VendorCode = request.VendorCode,
                VendorName = request.VendorName,
                SourceSystem = request.SourceSystem,
                SourceCode = request.SourceCode,
                ValidFrom = request.ValidFrom,
                ValidUntil = request.ValidUntil,
                Remark = request.Remark,
                CurrentStepId = request.CurrentStepId,
                CurrentStepName = currentStepName,
                Permissions = permissions,
                WorkflowSteps = portalWorkflowSteps,
                Documents = documents,
                Histories = histories
            };
        }

        public async Task<PortalRequestDetailDto?> GetPortalRequestByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var id = await _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Code == code)
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return id == 0 ? null : await GetPortalRequestByIdAsync(id, cancellationToken);
        }

        public async Task<PortalSaveResultDto> CreatePortalDraftAsync(SavePortalRequestDto input, CancellationToken cancellationToken = default)
        {
            var creatorUserId = _currentUserService.UserId;
            var sortedSteps = await GetSortedWorkflowStepsAsync(creatorUserId);

            for (var attempt = 1; attempt <= GenerateDocNoRetryLimit; attempt++)
            {
                using var transaction = _unitOfWork.BeginTransaction();
                try
                {
                    var newDocNo = await GenerateDocNoAsync();
                    var request = new Request
                    {
                        Code = newDocNo,
                        Title = string.IsNullOrWhiteSpace(input.Title) ? string.Empty : input.Title.Trim(),
                        RequestDate = _dateTime.Now,
                        Status = (int)RequestStatus.Draft,
                        CurrentStepId = 1,
                        CreatedBy = creatorUserId,
                        IsActive = true,
                        VendorCode = input.VendorCode ?? string.Empty,
                        VendorName = input.VendorName ?? string.Empty,
                        SourceSystem = input.SourceSystem,
                        SourceCode = input.SourceCode,
                        ValidFrom = input.ValidFrom,
                        ValidUntil = input.ValidUntil,
                        Remark = input.Remark
                    };

                    foreach (var step in sortedSteps)
                    {
                        request.ApprovalSteps.Add(new ApprovalStep
                        {
                            Sequence = step.SequenceNo,
                            StepName = step.StepName,
                            Status = (int)RequestStatus.Draft
                        });
                    }

                    await _unitOfWork.Repository<Request>().AddAsync(request);
                    await _unitOfWork.CommitAsync();
                    await transaction.CommitAsync();
                    await NotifyUpdatesAsync($"สร้างเอกสารร่างใหม่ {request.Code}");

                    return new PortalSaveResultDto
                    {
                        Id = request.Id,
                        Code = request.Code
                    };
                }
                catch (DbUpdateException ex) when (IsRequestCodeConflict(ex))
                {
                    await transaction.RollbackAsync();
                    _unitOfWork.ClearTrackedChanges();

                    if (attempt == GenerateDocNoRetryLimit)
                    {
                        throw new InvalidOperationException("Unable to generate a unique document number after multiple attempts.", ex);
                    }

                    _logger.LogWarning(ex, "Request code conflict on create portal draft attempt {Attempt}. Retrying.", attempt);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            throw new InvalidOperationException("Unable to create portal draft request.");
        }

        public async Task<PortalSaveResultDto> UpdatePortalDraftAsync(int id, SavePortalRequestDto input, CancellationToken cancellationToken = default)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request {id} not found.");
            }

            if (!string.Equals(request.CreatedBy, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("You do not have permission to edit this draft.");
            }

            if (request.Status != (int)RequestStatus.Draft)
            {
                throw new InvalidOperationException("Only draft requests can be updated.");
            }

            request.Title = string.IsNullOrWhiteSpace(input.Title) ? string.Empty : input.Title.Trim();
            request.VendorCode = input.VendorCode ?? string.Empty;
            request.VendorName = input.VendorName ?? string.Empty;
            request.SourceSystem = input.SourceSystem;
            request.SourceCode = input.SourceCode;
            request.ValidFrom = input.ValidFrom;
            request.ValidUntil = input.ValidUntil;
            request.Remark = input.Remark;

            await _unitOfWork.Repository<Request>().UpdateAsync(request);
            await _unitOfWork.CommitAsync();
            await NotifyUpdatesAsync($"แก้ไขเอกสารร่าง {request.Code}");

            return new PortalSaveResultDto
            {
                Id = request.Id,
                Code = request.Code
            };
        }

        public async Task SubmitPortalRequestAsync(int id, CancellationToken cancellationToken = default)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.Quotations)
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request {id} not found.");
            }

            if (!string.Equals(request.CreatedBy, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("You do not have permission to submit this request.");
            }

            if (request.Status != (int)RequestStatus.Draft)
            {
                throw new InvalidOperationException("Only draft requests can be submitted.");
            }

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                errors.Add("Title is required.");
            }
            if (string.IsNullOrWhiteSpace(request.VendorCode))
            {
                errors.Add("VendorCode is required.");
            }
            if (string.IsNullOrWhiteSpace(request.VendorName))
            {
                errors.Add("VendorName is required.");
            }
            if (request.ValidFrom == null)
            {
                errors.Add("ValidFrom date is required.");
            }
            if (request.ValidUntil == null)
            {
                errors.Add("ValidUntil date is required.");
            }
            if (request.ValidFrom != null && request.ValidUntil != null && request.ValidFrom > request.ValidUntil)
            {
                errors.Add("ValidFrom date cannot be after ValidUntil date.");
            }
            if (!request.Quotations.Any(q => q.DocumentTypeId == (int)DocumentType.OriginalQuotation))
            {
                errors.Add("At least one Original Quotation attachment (DocumentTypeId 10) is required before submit.");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"Submit validation failed: {string.Join(" ", errors)}");
            }

            await ApplyDraftSubmissionStateAsync(request, null);
            await _unitOfWork.Repository<Request>().UpdateAsync(request);
            await _unitOfWork.CommitAsync();
            await NotifyUpdatesAsync($"ยื่นขออนุมัติเอกสาร {request.Code}");
        }

        public async Task DeletePortalDraftAsync(int id, CancellationToken cancellationToken = default)
        {
            await DeleteAsync(id);
        }

        public async Task<PortalAttachmentDto> AddPortalAttachmentAsync(int requestId, UploadPortalAttachmentDto input, CancellationToken cancellationToken = default)
        {
            if (input.File == null || input.File.Length == 0)
            {
                throw new ArgumentException("Attachment file is required.");
            }

            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.Quotations)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request {requestId} not found.");
            }

            if (!string.Equals(request.CreatedBy, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("You do not have permission to modify attachments on this request.");
            }

            if (request.Status != (int)RequestStatus.Draft)
            {
                throw new InvalidOperationException("Attachments can only be added to draft requests.");
            }

            // No default. An omitted type used to become an Original Quotation, which is the one
            // type the submit rule requires, so a caller could satisfy that gate by accident.
            if (input.DocumentTypeId is not { } requestedTypeId ||
                !Enum.IsDefined(typeof(DocumentType), requestedTypeId))
            {
                throw new ArgumentException(
                    $"Unrecognised documentTypeId '{input.DocumentTypeId}'. Valid values are: " +
                    $"{(int)DocumentType.OriginalQuotation}, {(int)DocumentType.Comparison}, " +
                    $"{(int)DocumentType.Specifications}, {(int)DocumentType.Attachment}, " +
                    $"{(int)DocumentType.ExpiredQuotation}.");
            }

            int docTypeId = requestedTypeId;

            var files = new List<IFormFile> { input.File };
            var quotationJson = JsonSerializer.Serialize(new[]
            {
                new { FileName = input.File.FileName, DocumentTypeId = docTypeId }
            });

            var newQuotations = await _fileService.PrepareFilesForUploadAsync(files, quotationJson);
            var quotation = newQuotations.First();

            request.Quotations.Add(quotation);
            await _unitOfWork.Repository<Request>().UpdateAsync(request);
            await _unitOfWork.CommitAsync();
            await NotifyUpdatesAsync($"เพิ่มไฟล์แนบ {quotation.FileName} ในเอกสาร {request.Code}");

            string docTypeName = docTypeId switch
            {
                (int)DocumentType.OriginalQuotation => nameof(DocumentType.OriginalQuotation),
                (int)DocumentType.Comparison => nameof(DocumentType.Comparison),
                (int)DocumentType.Specifications => nameof(DocumentType.Specifications),
                (int)DocumentType.Attachment => nameof(DocumentType.Attachment),
                (int)DocumentType.ExpiredQuotation => nameof(DocumentType.ExpiredQuotation),
                _ => "Unknown"
            };

            return new PortalAttachmentDto
            {
                Id = quotation.Id,
                FileName = quotation.FileName,
                OriginalFileName = quotation.FileName,
                DocumentTypeId = quotation.DocumentTypeId,
                DocumentTypeName = docTypeName,
                FileSize = input.File.Length,
                UploadDate = _dateTime.Now,
                ViewUrl = $"/api/Request/ViewFile/{quotation.Id}"
            };
        }

        public async Task DeletePortalAttachmentAsync(int requestId, int attachmentId, CancellationToken cancellationToken = default)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request {requestId} not found.");
            }

            if (!string.Equals(request.CreatedBy, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("You do not have permission to delete attachments on this request.");
            }

            if (request.Status != (int)RequestStatus.Draft)
            {
                throw new InvalidOperationException("Attachments can only be deleted from draft requests.");
            }

            var quotation = request.Quotations.FirstOrDefault(q => q.Id == attachmentId);
            if (quotation == null)
            {
                throw new KeyNotFoundException($"Attachment {attachmentId} not found on request {requestId}.");
            }

            if (quotation.AttachmentFile != null)
            {
                await _unitOfWork.Repository<AttachmentFile>().DeleteAsync(quotation.AttachmentFile);
            }

            await _unitOfWork.Repository<Quotation>().DeleteAsync(quotation);
            await _unitOfWork.CommitAsync();
            await NotifyUpdatesAsync($"ลบไฟล์แนบ {quotation.FileName} ในเอกสาร {request.Code}");
        }

        public async Task ApprovePortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request {id} not found.");
            }

            if (request.Status != (int)RequestStatus.Pending)
            {
                throw new InvalidOperationException("Only pending requests can be approved.");
            }

            var currentUserId = _currentUserService.UserId;
            var currentStep = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == request.CurrentStepId);
            bool isAuthorizedApprover = false;

            if (currentStep != null && !string.IsNullOrEmpty(currentStep.ApproverNId))
            {
                isAuthorizedApprover = string.Equals(currentStep.ApproverNId, currentUserId, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                var workflowRoute = await _workflowService.GetWorkflowRouteDetailAsync(MainWorkflowId, request.CreatedBy);
                var permissions = _workflowService.GetPermissions(request, workflowRoute);
                isAuthorizedApprover = permissions.CanApprove;
            }

            if (!isAuthorizedApprover)
            {
                throw new UnauthorizedAccessException("You do not have permission to approve this request.");
            }

            await ApproveAsync(new ApprovalActionDto
            {
                RequestId = id,
                Comment = input.Comment ?? string.Empty
            });
        }

        public async Task RejectPortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request {id} not found.");
            }

            if (request.Status != (int)RequestStatus.Pending)
            {
                throw new InvalidOperationException("Only pending requests can be rejected.");
            }

            var currentUserId = _currentUserService.UserId;
            var currentStep = request.ApprovalSteps.FirstOrDefault(s => s.Sequence == request.CurrentStepId);
            bool isAuthorizedApprover = false;

            if (currentStep != null && !string.IsNullOrEmpty(currentStep.ApproverNId))
            {
                isAuthorizedApprover = string.Equals(currentStep.ApproverNId, currentUserId, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                var workflowRoute = await _workflowService.GetWorkflowRouteDetailAsync(MainWorkflowId, request.CreatedBy);
                var permissions = _workflowService.GetPermissions(request, workflowRoute);
                isAuthorizedApprover = permissions.CanReject;
            }

            if (!isAuthorizedApprover)
            {
                throw new UnauthorizedAccessException("You do not have permission to reject this request.");
            }

            await RejectAsync(new ApprovalActionDto
            {
                RequestId = id,
                Comment = input.Comment ?? string.Empty
            });
        }
    }
}