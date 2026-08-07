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

using QCS.Domain.DTOs.Integration;

namespace QCS.Application.Services
{
    public class PredecessorAlreadyRenewedException : Exception
    {
        public string QrsCode { get; }
        public string PreviousQcCode { get; }

        public PredecessorAlreadyRenewedException(string qrsCode, string previousQcCode)
            : base($"Quotation '{previousQcCode}' referenced by QRS request '{qrsCode}' has already been renewed.")
        {
            QrsCode = qrsCode;
            PreviousQcCode = previousQcCode;
        }
    }

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
        Task<AttachmentResultDto?> GetAttachmentAsync(int id);
        Task<int> GetMyPendingTaskCountAsync();
        Task<PortalPage<PortalRequestListItemDto>> GetPortalRequestsAsync(PortalRequestQuery query, CancellationToken cancellationToken = default);
        Task<PortalPage<RenewalCandidateDto>> GetRenewalCandidatesAsync(RenewalCandidateQuery query, CancellationToken cancellationToken = default);
        Task<PortalPage<IntegrationRenewalCandidateDto>> GetIntegrationRenewalCandidatesAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<IntegrationRenewalCandidateDto?> GetIntegrationRenewalCandidateByCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<PortalSetupResolutionDto> ResolveSetupFromQrsAsync(string qrsCode, CancellationToken cancellationToken = default);
        Task<PortalSetupResolutionDto> ResolveSetupFromQcsAsync(string qcCode, CancellationToken cancellationToken = default);
        Task<PortalRequestDetailDto?> GetPortalRequestByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<PortalRequestDetailDto?> GetPortalRequestByCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<PortalSaveResultDto> CreatePortalDraftAsync(SavePortalRequestDto input, CancellationToken cancellationToken = default);
        Task<PortalSaveResultDto> UpdatePortalDraftAsync(int id, SavePortalRequestDto input, CancellationToken cancellationToken = default);
        Task SubmitPortalRequestAsync(int id, CancellationToken cancellationToken = default);
        Task DeletePortalDraftAsync(int id, CancellationToken cancellationToken = default);
        Task<PortalAttachmentDto> AddPortalAttachmentAsync(int requestId, UploadPortalAttachmentDto input, CancellationToken cancellationToken = default);
        Task AddExpiredQuotationReferenceAsync(int requestId, AddExpiredQuotationReferenceDto input, CancellationToken cancellationToken = default);
        Task UpdatePortalDocumentsAsync(int requestId, UpdatePortalDocumentsDto input, CancellationToken cancellationToken = default);
        Task DeletePortalAttachmentAsync(int requestId, int attachmentId, CancellationToken cancellationToken = default);
        Task ApprovePortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default);
        Task RejectPortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default);
        Task ReturnPortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default);
        Task CancelPortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default);
        Task<ApprovalPreviewResult> GetRoutePreviewAsync(SavePortalRequestDto input, CancellationToken cancellationToken = default);
    }

    public class RequestService : IRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly IDateTime _dateTime;
        private readonly IFileService _fileService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<RequestService> _logger;
        private readonly IApprovalService _approvalService;
        private readonly IApprovalRequestFactory _approvalRequestFactory;
        private readonly IEmployeeDirectory _employeeDirectory;
        private readonly IQrsSourcingService _qrsSourcingService;

        private const string SignalREventName = "ReceiveUpdate";
        private const int GenerateDocNoRetryLimit = 3;
        private const string RequestCodeUniqueIndexName = "IX_Requests_Code";
        private const string RenewedFromRequestIdUniqueIndexName = "IX_Requests_RenewedFromRequestId";

        private static readonly Expression<Func<Request, RequestGridDto>> RequestGridProjection = r => new RequestGridDto
        {
            Id = r.Id,
            Code = r.Code,
            Title = r.Title,
            VendorCode = r.VendorCode,
            VendorName = r.VendorName,
            RequestDate = r.RequestDate,
            CurrentStepId = r.CurrentStepSequence ?? 0,
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
            CurrentStepId = r.CurrentStepSequence ?? 0,
            Remark = r.Remark ?? string.Empty,
            RequesterName = r.ApprovalSteps.Where(s => s.Sequence == 1).Select(s => s.ApproverName).FirstOrDefault() ?? "Unknown",
            RequesterNId = r.CreatedBy ?? string.Empty,
            ValidFrom = r.ValidFrom,
            ValidUntil = r.ValidUntil
        };

        public RequestService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            IDateTime dateTime,
            IFileService fileService,
            IHubContext<NotificationHub> hubContext,
            ILogger<RequestService> logger,
            IApprovalService approvalService,
            IApprovalRequestFactory approvalRequestFactory,
            IEmployeeDirectory employeeDirectory,
            IQrsSourcingService qrsSourcingService)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _dateTime = dateTime;
            _fileService = fileService;
            _hubContext = hubContext;
            _logger = logger;
            _approvalService = approvalService;
            _approvalRequestFactory = approvalRequestFactory;
            _employeeDirectory = employeeDirectory;
            _qrsSourcingService = qrsSourcingService;
        }

        private async Task DeleteLocalDraftAsync(int id)
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
            var result = new MyPendingTaskResult();
            try
            {
                var pendingDocIds = await _approvalService.ListPendingDocumentIdsAsync(currentUserId);
                if (pendingDocIds == null || pendingDocIds.Count == 0)
                {
                    return result;
                }

                var taskIds = await GetActiveRequestsQuery()
                    .AsNoTracking()
                    .Where(r => r.ApprovalDocumentId.HasValue && pendingDocIds.Contains(r.ApprovalDocumentId.Value))
                    .Select(r => r.Id)
                    .ToListAsync();

                result.TaskIds.AddRange(taskIds);
                result.TotalCount = taskIds.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch pending tasks from central approval service for user {UserId}", currentUserId);
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
                         && r.Status != (int)RequestStatus.Completed
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
                .Where(r => r.Status == (int)RequestStatus.Completed)
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
                    StatusName = ((RequestStatus)r.Status).ToString(),
                    CurrentStepId = r.CurrentStepSequence ?? 0,
                    CurrentStepName = r.CurrentStepName,
                    RequesterNId = r.CreatedBy ?? string.Empty,
                    RequesterName = r.CreatedBy ?? string.Empty,
                    ValidFrom = r.ValidFrom,
                    ValidUntil = r.ValidUntil,
                    Remark = r.Remark,
                    Documents = r.Quotations
                        .OrderBy(q => q.SortOrder)
                        .ThenBy(q => q.Id)
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
                .Where(r => r.CreatedBy == _currentUserService.UserId && r.Status == (int)RequestStatus.Completed)
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
                .Where(r => r.Status == (int)RequestStatus.InProcess)
                .Select(RequestGridWithRequesterProjection);
        }

        public IQueryable<RequestGridDto> GetAllApprovedRequestsQuery()
        {
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Completed)
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

            WorkflowRouteDetailDto workflowRoute;
            PermissionDto permissions;

            if (request.ApprovalDocumentId.HasValue)
            {
                var centralDetail = await _approvalService.GetDocumentAsync(
                    request.ApprovalDocumentId.Value,
                    _currentUserService.UserId)
                    ?? throw new InvalidOperationException(
                        $"Central approval document {request.ApprovalDocumentId.Value} was not found.");

                workflowRoute = new WorkflowRouteDetailDto
                {
                    Id = 0,
                    RouteName = "Central approval",
                    CanInitiate = false,
                    Steps = centralDetail.Steps.Select(step => new WorkflowStepDto
                    {
                        Id = 0,
                        SequenceNo = step.SequenceNo,
                        StepName = step.StepName,
                        Assignments = step.Assignees.Select(assignee => new AssignmentDto
                        {
                            NId = assignee.Username,
                            EmployeeName = assignee.EmployeeName ?? assignee.Username,
                            IsCurrentUser = string.Equals(
                                assignee.Username,
                                _currentUserService.UserId,
                                StringComparison.OrdinalIgnoreCase)
                        }).ToList()
                    }).OrderBy(s => s.SequenceNo).ToList()
                };

                var centralPermissions = centralDetail.Permissions;
                permissions = new PermissionDto
                {
                    CanSubmit = centralPermissions.CanSubmit,
                    CanApprove = centralPermissions.CanApprove,
                    CanReject = centralPermissions.CanReject,
                    CanReturn = centralPermissions.CanReturn,
                    CanCancel = centralPermissions.CanCancel,
                    IsCreator = centralPermissions.IsCreator,
                    IsCurrentAssignee = centralPermissions.IsCurrentAssignee,
                    AvailableActions = centralPermissions.AvailableActions.ToList()
                };
            }
            else
            {
                workflowRoute = new WorkflowRouteDetailDto
                {
                    Id = 0,
                    RouteName = "Legacy history",
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
                            ? new List<AssignmentDto>
                            {
                                new()
                                {
                                    NId = step.ApproverNId,
                                    EmployeeName = step.ApproverName ?? string.Empty,
                                    IsCurrentUser = string.Equals(
                                        step.ApproverNId,
                                        _currentUserService.UserId,
                                        StringComparison.OrdinalIgnoreCase)
                                }
                            }
                            : new List<AssignmentDto>()
                    }).OrderBy(step => step.SequenceNo).ToList()
                };
                permissions = new PermissionDto();
            }

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
                    .FirstOrDefault() ?? request.CreatedBy ?? string.Empty,
                CurrentStepId = request.CurrentStepSequence ?? 0,
                VendorCode = request.VendorCode,
                VendorName = request.VendorName,
                SourceSystem = request.SourceSystem,
                SourceCode = request.SourceCode,
                ValidFrom = request.ValidFrom,
                ValidUntil = request.ValidUntil,
                Remark = request.Remark,
                Quotations = request.Quotations
                    .OrderBy(q => q.SortOrder)
                    .ThenBy(q => q.Id)
                    .Select(q => new QuotationItemDto
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
                .Include(x => x.SourceQuotation).ThenInclude(source => source!.AttachmentFile)
                .FirstOrDefaultAsync(x => x.Id == fileId);

            if (q == null) return null;

            var attachment = q.AttachmentFile ?? q.SourceQuotation?.AttachmentFile;
            if (attachment?.Data != null)
            {
                return new AttachmentResultDto
                {
                    Data = attachment.Data,
                    ContentType = attachment.ContentType ?? "application/octet-stream",
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
                                 && r.Status != (int)RequestStatus.Completed
                                 && r.Status != (int)RequestStatus.Rejected);
                    break;

                case PortalRequestView.MyApproved:
                    requestsQuery = _unitOfWork.Repository<Request>().GetAll()
                        .AsNoTracking()
                        .Where(r => r.CreatedBy == currentUserId && r.Status == (int)RequestStatus.Completed);
                    break;

                case PortalRequestView.Rejected:
                    requestsQuery = _unitOfWork.Repository<Request>().GetAll()
                        .AsNoTracking()
                        .Where(r => r.CreatedBy == currentUserId && r.Status == (int)RequestStatus.Rejected);
                    break;

                case PortalRequestView.AllApproved:
                    requestsQuery = _unitOfWork.Repository<Request>().GetAll()
                        .AsNoTracking()
                        .Where(r => r.Status == (int)RequestStatus.Completed);
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
                    CurrentStepId = r.CurrentStepSequence ?? 0,
                    Status = r.Status,
                    StatusName = ((RequestStatus)r.Status).ToString(),
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
                .Include(r => r.Quotations).ThenInclude(q => q.SourceQuotation).ThenInclude(source => source!.Request)
                .Include(r => r.ApprovalSteps)
                .Include(r => r.RenewedFromRequest)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null) return null;

            var currentUserId = _currentUserService.UserId;

            ApprovalDocumentDetail? centralDetail = null;
            if (request.ApprovalDocumentId.HasValue)
            {
                centralDetail = await _approvalService.GetDocumentAsync(
                    request.ApprovalDocumentId.Value,
                    currentUserId,
                    cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Central approval document {request.ApprovalDocumentId.Value} was not found.");

                var isParticipant = centralDetail.Permissions.IsCreator
                    || centralDetail.Permissions.IsCurrentAssignee
                    || centralDetail.Steps.Any(step => step.Assignees.Any(assignee =>
                        string.Equals(assignee.Username, currentUserId, StringComparison.OrdinalIgnoreCase)));

                if (!isParticipant)
                {
                    throw new UnauthorizedAccessException("You do not have permission to view this request.");
                }

                request.ApprovalDocumentNumber = centralDetail.Summary.DocumentNumber;
                request.Status = (int)centralDetail.Summary.Status;
                request.CurrentStepSequence = centralDetail.Summary.CurrentStepSequence;
                request.CurrentStepName = centralDetail.Summary.CurrentStepName;
                request.StatusSyncedAt = _dateTime.Now;
                await _unitOfWork.CommitAsync();
            }

            var requesterNId = request.CreatedBy ?? string.Empty;
            var empDetails = await _employeeDirectory.GetEmployeeDetailsAsync(requesterNId, cancellationToken);
            var requesterName = empDetails?.EmployeeName ?? requesterNId;

            var statusEnum = (RequestStatus)request.Status;
            var statusName = statusEnum.ToString();

            PermissionDto permissions;
            List<PortalWorkflowStepDto> portalWorkflowSteps = new();
            List<PortalHistoryDto> histories = new();

            if (centralDetail != null)
            {
                var cp = centralDetail.Permissions;
                permissions = new PermissionDto
                {
                    CanSubmit = cp.CanSubmit,
                    CanApprove = cp.CanApprove,
                    CanReject = cp.CanReject,
                    CanReturn = cp.CanReturn,
                    CanCancel = cp.CanCancel,
                    CanEdit = statusEnum == RequestStatus.Draft && string.Equals(request.CreatedBy, currentUserId, StringComparison.OrdinalIgnoreCase),
                    CanDelete = statusEnum == RequestStatus.Draft && string.Equals(request.CreatedBy, currentUserId, StringComparison.OrdinalIgnoreCase),
                    IsCreator = cp.IsCreator,
                    IsCurrentAssignee = cp.IsCurrentAssignee,
                    AvailableActions = cp.AvailableActions.ToList()
                };

                foreach (var step in centralDetail.Steps)
                {
                    portalWorkflowSteps.Add(new PortalWorkflowStepDto
                    {
                        SequenceNo = step.SequenceNo,
                        StepName = step.StepName,
                        StatusName = step.Status ?? "Pending",
                        IsCurrentStep = step.SequenceNo == (centralDetail.Summary.CurrentStepSequence ?? request.CurrentStepSequence),
                        Assignments = step.Assignees.Select(a => new PortalAssignmentDto
                        {
                            NId = a.Username,
                            EmployeeName = a.EmployeeName ?? a.Username,
                            IsCurrentUser = string.Equals(a.Username, currentUserId, StringComparison.OrdinalIgnoreCase)
                        }).ToList()
                    });
                }

                foreach (var h in centralDetail.History)
                {
                    histories.Add(new PortalHistoryDto
                    {
                        ActionDate = h.CreatedAt,
                        ApproverNId = h.ActorName ?? string.Empty,
                        ApproverName = h.ActorName ?? string.Empty,
                        StepName = h.StepName ?? string.Empty,
                        Comment = h.Reason
                    });
                }
            }
            else
            {
                var isCreator = string.Equals(request.CreatedBy, currentUserId, StringComparison.OrdinalIgnoreCase);
                var isApproved = statusEnum == RequestStatus.Completed;
                var isAssigned = request.ApprovalSteps.Any(s => string.Equals(s.ApproverNId, currentUserId, StringComparison.OrdinalIgnoreCase));

                if (!isCreator && !isApproved && !isAssigned)
                {
                    throw new UnauthorizedAccessException("You do not have permission to view this request.");
                }

                permissions = new PermissionDto
                {
                    CanEdit = statusEnum == RequestStatus.Draft && isCreator,
                    CanDelete = statusEnum == RequestStatus.Draft && isCreator,
                    IsCreator = isCreator
                };

                foreach (var step in request.ApprovalSteps.OrderBy(s => s.Sequence))
                {
                    portalWorkflowSteps.Add(new PortalWorkflowStepDto
                    {
                        SequenceNo = step.Sequence,
                        StepName = step.StepName,
                        StatusName = ((LegacyApprovalStepStatus)step.Status).ToString(),
                        IsCurrentStep = step.Sequence == request.CurrentStepSequence,
                        ApproverNId = step.ApproverNId,
                        ApproverName = step.ApproverName,
                        ActionDate = step.ActionDate,
                        Comment = step.Comment
                    });
                }

                foreach (var s in request.ApprovalSteps.Where(step => step.ActionDate != null || step.Status == (int)LegacyApprovalStepStatus.Approved || step.Status == (int)LegacyApprovalStepStatus.Rejected).OrderBy(step => step.Sequence))
                {
                    histories.Add(new PortalHistoryDto
                    {
                        SequenceNo = s.Sequence,
                        StepName = s.StepName,
                        Status = s.Status,
                        StatusName = ((LegacyApprovalStepStatus)s.Status).ToString(),
                        ApproverNId = s.ApproverNId,
                        ApproverName = s.ApproverName,
                        ActionDate = s.ActionDate,
                        Comment = s.Comment
                    });
                }
            }

            var documents = request.Quotations
                .OrderBy(q => q.SortOrder)
                .ThenBy(q => q.Id)
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
                    SortOrder = q.SortOrder,
                    ReferenceCode = q.SourceQuotation?.Request.Code,
                    FileSize = q.FileSize,
                    ViewUrl = $"/api/Request/ViewFile/{q.Id}"
                }).ToList();

            if (statusEnum == RequestStatus.Completed)
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

            var canRenew = await GetEligibleRenewalCandidatesQuery()
                .AnyAsync(candidate => candidate.Id == request.Id, cancellationToken);

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
                Intent = request.Intent,
                IntentName = request.Intent.ToString(),
                RenewedFromRequestId = request.RenewedFromRequestId,
                RenewedFromCode = request.RenewedFromRequest?.Code,
                OriginName = string.Equals(request.SourceSystem, "QRS", StringComparison.OrdinalIgnoreCase) ? "QRS" : "QCS",
                ValidFrom = request.ValidFrom,
                ValidUntil = request.ValidUntil,
                Remark = request.Remark,
                ApprovalDocumentId = request.ApprovalDocumentId,
                ApprovalDocumentNumber = request.ApprovalDocumentNumber,
                CurrentStepSequence = request.CurrentStepSequence,
                CurrentStepId = request.CurrentStepSequence ?? 0,
                CurrentStepName = centralDetail?.Summary.CurrentStepName ?? request.CurrentStepName,
                CanRenew = canRenew,
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

        private IQueryable<Request> GetEligibleRenewalCandidatesQuery()
        {
            var maxValidUntil = _dateTime.Now.AddDays(30);

            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Completed
                    && r.ValidUntil.HasValue
                    && r.ValidUntil <= maxValidUntil
                    && r.Quotations.Any(q => q.DocumentTypeId == (int)DocumentType.OriginalQuotation && q.SourceQuotationId == null && q.AttachmentFileId.HasValue)
                    && !_unitOfWork.Repository<Request>().GetAll().Any(child => child.RenewedFromRequestId == r.Id));
        }

        // The adapter already rejects unknown enum values, so this is the second belt.
        // It throws the same contract-violation exception rather than a plain
        // InvalidOperationException: contract C says an invalid upstream type/intent is a
        // 502, and a 400 here would blame the caller for QRS sending us something we
        // cannot read.
        private static void ValidateQrsSourcingContract(QrsSourcingDetailDto detail)
        {
            if (!Enum.IsDefined(typeof(QrsRequestType), detail.RequestType))
            {
                throw new QrsSourcingException(
                    $"QRS request '{detail.Code}' has unrecognized request type '{detail.RequestType}'.",
                    isContractViolation: true);
            }

            if (!Enum.IsDefined(typeof(QrsRequestIntent), detail.Intent))
            {
                throw new QrsSourcingException(
                    $"QRS request '{detail.Code}' has unrecognized intent '{detail.Intent}'.",
                    isContractViolation: true);
            }
        }

        public async Task<PortalPage<RenewalCandidateDto>> GetRenewalCandidatesAsync(RenewalCandidateQuery query, CancellationToken cancellationToken = default)
        {
            var now = _dateTime.Now;
            var baseQuery = GetEligibleRenewalCandidatesQuery();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                baseQuery = baseQuery.Where(r => EF.Functions.Like(r.Code, $"%{search}%")
                    || EF.Functions.Like(r.Title, $"%{search}%")
                    || EF.Functions.Like(r.VendorCode, $"%{search}%")
                    || EF.Functions.Like(r.VendorName, $"%{search}%")
                    || (r.SourceCode != null && EF.Functions.Like(r.SourceCode, $"%{search}%")));
            }

            var totalCount = await baseQuery.CountAsync(cancellationToken);
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var items = await baseQuery
                .OrderBy(r => r.ValidUntil)
                .ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RenewalCandidateDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    ValidFrom = r.ValidFrom,
                    ValidUntil = r.ValidUntil,
                    SourceSystem = r.SourceSystem,
                    SourceCode = r.SourceCode,
                    RequestDate = r.RequestDate,
                    OriginalQuotationCount = r.Quotations.Count(q => q.DocumentTypeId == (int)DocumentType.OriginalQuotation && q.SourceQuotationId == null && q.AttachmentFileId.HasValue),
                    RenewalWindowStatus = r.ValidUntil < now ? "Expired" : "ExpiringSoon"
                })
                .ToListAsync(cancellationToken);

            return new PortalPage<RenewalCandidateDto>(items, totalCount, page, pageSize);
        }

        public async Task<PortalPage<IntegrationRenewalCandidateDto>> GetIntegrationRenewalCandidatesAsync(
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var now = _dateTime.Now;
            var baseQuery = GetEligibleRenewalCandidatesQuery();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                baseQuery = baseQuery.Where(r => EF.Functions.Like(r.Code, $"%{trimmedSearch}%")
                    || EF.Functions.Like(r.Title, $"%{trimmedSearch}%")
                    || EF.Functions.Like(r.VendorCode, $"%{trimmedSearch}%")
                    || EF.Functions.Like(r.VendorName, $"%{trimmedSearch}%"));
            }

            var totalCount = await baseQuery.CountAsync(cancellationToken);
            var validPage = Math.Max(1, page);
            var validPageSize = Math.Clamp(pageSize <= 0 ? 10 : pageSize, 1, 100);

            var items = await baseQuery
                .OrderBy(r => r.ValidUntil)
                .ThenByDescending(r => r.Id)
                .Skip((validPage - 1) * validPageSize)
                .Take(validPageSize)
                .Select(r => new IntegrationRenewalCandidateDto
                {
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    ValidUntil = r.ValidUntil,
                    RenewalWindowStatus = r.ValidUntil < now ? "Expired" : "ExpiringSoon"
                })
                .ToListAsync(cancellationToken);

            return new PortalPage<IntegrationRenewalCandidateDto>(items, totalCount, validPage, validPageSize);
        }

        public async Task<IntegrationRenewalCandidateDto?> GetIntegrationRenewalCandidateByCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var now = _dateTime.Now;
            var trimmedCode = code.Trim();

            return await GetEligibleRenewalCandidatesQuery()
                .Where(r => r.Code == trimmedCode)
                .Select(r => new IntegrationRenewalCandidateDto
                {
                    Code = r.Code,
                    Title = r.Title,
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName,
                    ValidUntil = r.ValidUntil,
                    RenewalWindowStatus = r.ValidUntil < now ? "Expired" : "ExpiringSoon"
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PortalSetupResolutionDto> ResolveSetupFromQrsAsync(string qrsCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(qrsCode))
            {
                throw new KeyNotFoundException("QRS source code is required.");
            }

            var trimmedCode = qrsCode.Trim();
            var qrsDetail = await _qrsSourcingService.GetByCodeAsync(trimmedCode, cancellationToken);
            if (qrsDetail == null)
            {
                throw new KeyNotFoundException($"QRS request '{trimmedCode}' was not found.");
            }

            ValidateQrsSourcingContract(qrsDetail);

            if (qrsDetail.Intent == (int)QrsRequestIntent.New)
            {
                return new PortalSetupResolutionDto
                {
                    Flow = "NewQrs",
                    Intent = 0,
                    Origin = "QRS",
                    SourceCode = qrsDetail.Code,
                    SourceTitle = qrsDetail.Title,
                    RenewedFromRequestId = null,
                    RenewedFromCode = null,
                    VendorCode = string.Empty,
                    VendorName = string.Empty
                };
            }

            if (qrsDetail.Intent == (int)QrsRequestIntent.Renewal)
            {
                if (string.IsNullOrWhiteSpace(qrsDetail.PreviousQcCode))
                {
                    throw new KeyNotFoundException($"QRS renewal request '{trimmedCode}' is missing PreviousQcCode.");
                }

                var previousQcCode = qrsDetail.PreviousQcCode.Trim();
                var predecessor = await _unitOfWork.Repository<Request>().GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Code == previousQcCode, cancellationToken);

                if (predecessor == null)
                {
                    throw new KeyNotFoundException($"Previous quotation '{previousQcCode}' referenced by QRS request '{trimmedCode}' is not found or not eligible for renewal.");
                }

                var hasSuccessor = await _unitOfWork.Repository<Request>().GetAll()
                    .AsNoTracking()
                    .AnyAsync(child => child.RenewedFromRequestId == predecessor.Id, cancellationToken);

                if (hasSuccessor)
                {
                    throw new PredecessorAlreadyRenewedException(trimmedCode, previousQcCode);
                }

                var isEligible = await GetEligibleRenewalCandidatesQuery()
                    .AnyAsync(candidate => candidate.Id == predecessor.Id, cancellationToken);
                if (!isEligible)
                {
                    throw new KeyNotFoundException($"Previous quotation '{previousQcCode}' referenced by QRS request '{trimmedCode}' is not found or not eligible for renewal.");
                }

                return new PortalSetupResolutionDto
                {
                    Flow = "RenewalQrs",
                    Intent = 1,
                    Origin = "QRS",
                    SourceCode = qrsDetail.Code,
                    SourceTitle = qrsDetail.Title,
                    RenewedFromRequestId = predecessor.Id,
                    RenewedFromCode = predecessor.Code,
                    VendorCode = predecessor.VendorCode,
                    VendorName = predecessor.VendorName
                };
            }

            throw new QrsSourcingException(
                $"QRS request '{trimmedCode}' has unrecognized intent '{qrsDetail.Intent}'.",
                isContractViolation: true);
        }

        public async Task<PortalSetupResolutionDto> ResolveSetupFromQcsAsync(string qcCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(qcCode))
            {
                throw new KeyNotFoundException("Quotation code is required.");
            }

            var trimmedCode = qcCode.Trim();
            var candidate = await GetEligibleRenewalCandidatesQuery()
                .FirstOrDefaultAsync(r => r.Code == trimmedCode, cancellationToken);

            if (candidate == null)
            {
                throw new KeyNotFoundException($"Quotation '{trimmedCode}' is not found or not eligible for renewal.");
            }

            return new PortalSetupResolutionDto
            {
                Flow = "RenewalQcs",
                Intent = 1,
                Origin = "QCS",
                SourceCode = null,
                SourceTitle = null,
                RenewedFromRequestId = candidate.Id,
                RenewedFromCode = candidate.Code,
                VendorCode = candidate.VendorCode,
                VendorName = candidate.VendorName
            };
        }

        private static void ValidateSetupMatrix(SavePortalRequestDto input)
        {
            if (!Enum.IsDefined(typeof(RequestIntent), input.Intent))
            {
                throw new ArgumentException($"Invalid RequestIntent '{input.Intent}'.");
            }

            var isQrs = string.Equals(input.SourceSystem, "QRS", StringComparison.OrdinalIgnoreCase);
            var hasSourceSystem = !string.IsNullOrWhiteSpace(input.SourceSystem);
            var hasSourceCode = !string.IsNullOrWhiteSpace(input.SourceCode);

            if (hasSourceSystem && !isQrs)
            {
                throw new ArgumentException($"Invalid SourceSystem '{input.SourceSystem}'.");
            }

            if (isQrs && !hasSourceCode)
            {
                throw new ArgumentException("SourceCode is required when SourceSystem is 'QRS'.");
            }

            if (!isQrs && (hasSourceSystem || hasSourceCode))
            {
                throw new ArgumentException("SourceSystem and SourceCode must both be null/blank for QCS origin.");
            }

            if (input.Intent == RequestIntent.New)
            {
                if (input.RenewedFromRequestId.HasValue)
                {
                    throw new ArgumentException("RenewedFromRequestId must be null when Intent is New.");
                }
            }
            else if (input.Intent == RequestIntent.Renewal)
            {
                if (!isQrs && !input.RenewedFromRequestId.HasValue)
                {
                    throw new ArgumentException("RenewedFromRequestId is required when Intent is Renewal for QCS origin.");
                }
            }
        }

        private static bool IsPredecessorUniqueConflict(DbUpdateException exception)
        {
            return exception.InnerException?.Message.Contains(RenewedFromRequestIdUniqueIndexName, StringComparison.OrdinalIgnoreCase) == true
                || exception.Message.Contains(RenewedFromRequestIdUniqueIndexName, StringComparison.OrdinalIgnoreCase);
        }

        private void ValidatePersistedSetupForSubmission(Request request)
        {
            if (!Enum.IsDefined(typeof(RequestIntent), request.Intent))
            {
                throw new InvalidOperationException($"Request setup has an invalid intent '{request.Intent}'.");
            }

            var isQrs = string.Equals(request.SourceSystem, "QRS", StringComparison.OrdinalIgnoreCase);
            var hasSourceSystem = !string.IsNullOrWhiteSpace(request.SourceSystem);
            var hasSourceCode = !string.IsNullOrWhiteSpace(request.SourceCode);

            if ((hasSourceSystem && !isQrs) || (isQrs && !hasSourceCode) || (!isQrs && (hasSourceSystem || hasSourceCode)))
            {
                throw new InvalidOperationException("Request setup origin is no longer coherent.");
            }

            if (request.Intent == RequestIntent.New)
            {
                if (request.RenewedFromRequestId.HasValue)
                {
                    throw new InvalidOperationException("A new request cannot have a renewal predecessor.");
                }

                return;
            }

            var predecessor = request.RenewedFromRequest;
            if (!request.RenewedFromRequestId.HasValue || predecessor == null || predecessor.Id == request.Id)
            {
                throw new InvalidOperationException("The renewal predecessor no longer exists or is invalid.");
            }

            if (predecessor.Status != (int)RequestStatus.Completed)
            {
                throw new InvalidOperationException("The renewal predecessor no longer satisfies the request setup.");
            }

            if (!string.Equals(request.VendorCode, predecessor.VendorCode, StringComparison.Ordinal) ||
                !string.Equals(request.VendorName, predecessor.VendorName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The renewal vendor no longer matches its predecessor.");
            }

            var consumedByOther = _unitOfWork.Repository<Request>().GetAll().AsNoTracking()
                .Any(r => r.RenewedFromRequestId == predecessor.Id && r.Id != request.Id);
            if (consumedByOther)
            {
                throw new InvalidOperationException("The previous quotation was already renewed by another request.");
            }
        }

        public async Task<PortalSaveResultDto> CreatePortalDraftAsync(SavePortalRequestDto input, CancellationToken cancellationToken = default)
        {
            ValidateSetupMatrix(input);
            var creatorUserId = _currentUserService.UserId;

            Request? predecessor = null;
            int? renewedFromRequestId = input.RenewedFromRequestId;

            if (input.Intent == RequestIntent.Renewal)
            {
                var isQrs = string.Equals(input.SourceSystem, "QRS", StringComparison.OrdinalIgnoreCase);
                if (isQrs)
                {
                    var qrsDetail = await _qrsSourcingService.GetByCodeAsync(input.SourceCode!, cancellationToken);
                    if (qrsDetail == null)
                    {
                        throw new KeyNotFoundException($"Referenced QRS request {input.SourceCode} not found.");
                    }

                    ValidateQrsSourcingContract(qrsDetail);
                    if (qrsDetail.Intent != (int)QrsRequestIntent.Renewal || string.IsNullOrWhiteSpace(qrsDetail.PreviousQcCode))
                    {
                        throw new InvalidOperationException("Referenced QRS request is not a valid renewal request.");
                    }

                    var previousQcCode = qrsDetail.PreviousQcCode.Trim();
                    predecessor = await _unitOfWork.Repository<Request>().GetAll()
                        .Include(r => r.Quotations)
                        .FirstOrDefaultAsync(r => r.Code == previousQcCode, cancellationToken);

                    if (predecessor == null)
                    {
                        throw new KeyNotFoundException($"Referenced renewal request {previousQcCode} not found.");
                    }
                }
                else
                {
                    var predecessorId = input.RenewedFromRequestId!.Value;
                    predecessor = await _unitOfWork.Repository<Request>().GetAll()
                        .Include(r => r.Quotations)
                        .FirstOrDefaultAsync(r => r.Id == predecessorId, cancellationToken);

                    if (predecessor == null)
                    {
                        throw new KeyNotFoundException($"Referenced renewal request {predecessorId} not found.");
                    }
                }

                var hasSuccessor = await _unitOfWork.Repository<Request>().GetAll().AsNoTracking()
                    .AnyAsync(r => r.RenewedFromRequestId == predecessor.Id, cancellationToken);
                if (hasSuccessor)
                {
                    throw new PredecessorAlreadyRenewedException(input.SourceCode ?? string.Empty, predecessor.Code);
                }

                var isEligible = await GetEligibleRenewalCandidatesQuery()
                    .AnyAsync(candidate => candidate.Id == predecessor.Id, cancellationToken);
                if (!isEligible)
                {
                    throw new InvalidOperationException("The referenced renewal request is not eligible for renewal.");
                }

                renewedFromRequestId = predecessor.Id;
            }

            var vendorCode = input.Intent == RequestIntent.Renewal
                ? (predecessor!.VendorCode ?? string.Empty)
                : (input.VendorCode ?? string.Empty);
            var vendorName = input.Intent == RequestIntent.Renewal
                ? (predecessor!.VendorName ?? string.Empty)
                : (input.VendorName ?? string.Empty);

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
                        CurrentStepSequence = null,
                        CreatedBy = creatorUserId,
                        IsActive = true,
                        Intent = input.Intent,
                        RenewedFromRequestId = renewedFromRequestId,
                        VendorCode = vendorCode,
                        VendorName = vendorName,
                        SourceSystem = string.Equals(input.SourceSystem, "QRS", StringComparison.OrdinalIgnoreCase) ? "QRS" : null,
                        SourceCode = string.Equals(input.SourceSystem, "QRS", StringComparison.OrdinalIgnoreCase) ? input.SourceCode?.Trim() : null,
                        ValidFrom = input.ValidFrom,
                        ValidUntil = input.ValidUntil,
                        Remark = input.Remark
                    };

                    if (input.Intent == RequestIntent.Renewal && predecessor != null)
                    {
                        AddExpiredQuotationReferencesFromSource(request, predecessor);
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
                catch (DbUpdateException ex) when (IsPredecessorUniqueConflict(ex))
                {
                    await transaction.RollbackAsync();
                    _unitOfWork.ClearTrackedChanges();
                    throw new PredecessorAlreadyRenewedException(input.SourceCode ?? string.Empty, predecessor?.Code ?? string.Empty);
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

            var requestSourceSys = request.SourceSystem ?? string.Empty;
            var inputSourceSys = input.SourceSystem ?? string.Empty;
            var requestSourceCode = request.SourceCode ?? string.Empty;
            var inputSourceCode = input.SourceCode ?? string.Empty;

            if (request.Intent != input.Intent ||
                request.RenewedFromRequestId != input.RenewedFromRequestId ||
                !string.Equals(requestSourceSys, inputSourceSys, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(requestSourceCode, inputSourceCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Request setup context (Intent, Origin, RenewedFromRequestId) cannot be modified after creation.");
            }

            request.Title = string.IsNullOrWhiteSpace(input.Title) ? string.Empty : input.Title.Trim();
            if (request.Intent == RequestIntent.New)
            {
                request.VendorCode = input.VendorCode ?? string.Empty;
                request.VendorName = input.VendorName ?? string.Empty;
            }
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
                .Include(r => r.RenewedFromRequest)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null) throw new KeyNotFoundException($"Request {id} not found.");
            if (!string.Equals(request.CreatedBy, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("You do not have permission to submit this request.");
            if (request.Status != (int)RequestStatus.Draft && request.Status != (int)RequestStatus.Returned)
                throw new InvalidOperationException("Only draft or returned requests can be submitted.");

            ValidatePersistedSetupForSubmission(request);

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(request.Title)) errors.Add("Title is required.");
            if (string.IsNullOrWhiteSpace(request.VendorCode)) errors.Add("VendorCode is required.");
            if (string.IsNullOrWhiteSpace(request.VendorName)) errors.Add("VendorName is required.");
            if (request.ValidFrom == null) errors.Add("ValidFrom date is required.");
            if (request.ValidUntil == null) errors.Add("ValidUntil date is required.");
            if (request.ValidFrom != null && request.ValidUntil != null && request.ValidFrom > request.ValidUntil)
                errors.Add("ValidFrom date cannot be after ValidUntil date.");
            if (!request.Quotations.Any(q => q.DocumentTypeId == (int)DocumentType.OriginalQuotation && q.SourceQuotationId == null && q.AttachmentFileId.HasValue))
                errors.Add("At least one Original Quotation attachment (DocumentTypeId 10) is required before submit.");

            if (errors.Count > 0)
                throw new InvalidOperationException($"Submit validation failed: {string.Join(" ", errors)}");

            var actingNId = _currentUserService.UserId;
            var requesterOrgCode = await ResolveRequesterOrgCodeAsync(actingNId, cancellationToken);

            var docRequest = _approvalRequestFactory.Build(new ApprovalRequestContext(
                request.Title,
                request.Code,
                request.Id,
                requesterOrgCode,
                request.VendorCode,
                request.ValidFrom,
                request.ValidUntil,
                request.Quotations.Count));

            ApprovalDocumentSummary summary;

            if (request.ApprovalDocumentId.HasValue)
            {
                var existingDetail = await _approvalService.GetDocumentAsync(
                    request.ApprovalDocumentId.Value,
                    actingNId,
                    cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Central approval document {request.ApprovalDocumentId.Value} was not found.");

                summary = CanSubmitCentralStatus(existingDetail.Summary.Status)
                    ? await _approvalService.SubmitDocumentAsync(request.ApprovalDocumentId.Value, actingNId, cancellationToken)
                    : existingDetail.Summary;
            }
            else
            {
                var existing = await _approvalService.FindBySourceAsync(request.Code, cancellationToken);
                if (existing != null)
                {
                    request.ApprovalDocumentId = existing.Id;
                    request.ApprovalDocumentNumber = existing.DocumentNumber;
                    summary = CanSubmitCentralStatus(existing.Status)
                        ? await _approvalService.SubmitDocumentAsync(existing.Id, actingNId, cancellationToken)
                        : existing;
                }
                else
                {
                    summary = await _approvalService.CreateDocumentAsync(docRequest, actingNId, cancellationToken);
                }
            }

            request.ApprovalDocumentId = summary.Id;
            request.ApprovalDocumentNumber = summary.DocumentNumber;
            request.Status = (int)summary.Status;
            request.CurrentStepSequence = summary.CurrentStepSequence;
            request.CurrentStepName = summary.CurrentStepName;
            request.StatusSyncedAt = _dateTime.Now;

            await _unitOfWork.Repository<Request>().UpdateAsync(request);
            await _unitOfWork.CommitAsync();
            await NotifyUpdatesAsync($"ยื่นขออนุมัติเอกสาร {request.Code}");
        }

        private static bool CanSubmitCentralStatus(RequestStatus status) =>
            status is RequestStatus.Draft or RequestStatus.Returned;

        public async Task DeletePortalDraftAsync(int id, CancellationToken cancellationToken = default)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null)
                throw new KeyNotFoundException($"Request {id} not found.");
            if (request.Status != (int)RequestStatus.Draft)
                throw new InvalidOperationException("Only draft requests can be deleted.");
            if (!string.Equals(request.CreatedBy, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("You can only delete your own draft requests.");

            if (request.ApprovalDocumentId.HasValue)
            {
                await _approvalService.DeleteDraftAsync(
                    request.ApprovalDocumentId.Value,
                    _currentUserService.UserId,
                    cancellationToken);
            }

            await DeleteLocalDraftAsync(id);
        }

        public async Task<PortalAttachmentDto> AddPortalAttachmentAsync(int requestId, UploadPortalAttachmentDto input, CancellationToken cancellationToken = default)
        {
            if (input.File == null || input.File.Length == 0)
            {
                throw new ArgumentException("Attachment file is required.");
            }

            if (!string.Equals(Path.GetExtension(input.File.FileName), ".pdf", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(input.File.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only PDF attachments are allowed.");
            }

            var header = new byte[5];
            var bytesRead = 0;
            await using (var stream = input.File.OpenReadStream())
            {
                while (bytesRead < header.Length)
                {
                    var read = await stream.ReadAsync(header.AsMemory(bytesRead), cancellationToken);
                    if (read == 0) break;
                    bytesRead += read;
                }
            }

            if (bytesRead != header.Length || !header.AsSpan().SequenceEqual("%PDF-"u8))
            {
                throw new ArgumentException("The attachment content is not a valid PDF file.");
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
            quotation.SortOrder = request.Quotations.Count == 0
                ? 1
                : request.Quotations.Max(q => q.SortOrder) + 1;

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
                SortOrder = quotation.SortOrder,
                FileSize = input.File.Length,
                UploadDate = _dateTime.Now,
                ViewUrl = $"/api/Request/ViewFile/{quotation.Id}"
            };
        }

        private void AddExpiredQuotationReferencesFromSource(Request targetRequest, Request sourceRequest)
        {
            if (sourceRequest.Id == targetRequest.Id)
            {
                throw new InvalidOperationException("A request cannot reference itself.");
            }

            if (sourceRequest.Status != (int)RequestStatus.Completed)
            {
                throw new InvalidOperationException("The referenced request must be Completed.");
            }

            if (string.IsNullOrWhiteSpace(targetRequest.VendorCode) ||
                !string.Equals(targetRequest.VendorCode.Trim(), sourceRequest.VendorCode?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The referenced request must use the same Vendor Code.");
            }

            var sourceQuotations = sourceRequest.Quotations
                .Where(quotation => quotation.DocumentTypeId == (int)DocumentType.OriginalQuotation
                    && quotation.SourceQuotationId == null
                    && quotation.AttachmentFileId.HasValue)
                .OrderBy(quotation => quotation.SortOrder)
                .ThenBy(quotation => quotation.Id)
                .ToList();

            if (sourceQuotations.Count == 0)
            {
                throw new InvalidOperationException("The referenced request has no Original Quotation PDF.");
            }

            var sourceIds = sourceQuotations.Select(quotation => quotation.Id).ToHashSet();
            if (targetRequest.Quotations.Any(quotation => quotation.SourceQuotationId.HasValue
                && sourceIds.Contains(quotation.SourceQuotationId.Value)))
            {
                throw new InvalidOperationException($"Request {sourceRequest.Code} is already referenced.");
            }

            var nextSortOrder = targetRequest.Quotations.Count == 0
                ? 1
                : targetRequest.Quotations.Max(quotation => quotation.SortOrder) + 1;

            foreach (var sourceQuotation in sourceQuotations)
            {
                targetRequest.Quotations.Add(new Quotation
                {
                    FileName = sourceQuotation.FileName,
                    FilePath = "Reference",
                    ContentType = sourceQuotation.ContentType,
                    FileSize = sourceQuotation.FileSize,
                    DocumentTypeId = (int)DocumentType.ExpiredQuotation,
                    SortOrder = nextSortOrder++,
                    SourceQuotationId = sourceQuotation.Id
                });
            }
        }

        public async Task AddExpiredQuotationReferenceAsync(int requestId, AddExpiredQuotationReferenceDto input, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
            {
                throw new ArgumentException("Expired quotation Code is required.");
            }

            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(target => target.Quotations)
                .FirstOrDefaultAsync(target => target.Id == requestId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request {requestId} not found.");
            }

            if (!string.Equals(request.CreatedBy, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("You do not have permission to add quotation references to this request.");
            }

            if (request.Status != (int)RequestStatus.Draft)
            {
                throw new InvalidOperationException("Quotation references can only be added to draft requests.");
            }

            var normalizedCode = input.Code.Trim().ToUpperInvariant();
            var sourceRequest = await _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Include(source => source.Quotations)
                .FirstOrDefaultAsync(source => source.Code == normalizedCode, cancellationToken);

            if (sourceRequest == null)
            {
                throw new KeyNotFoundException($"Request {normalizedCode} not found.");
            }

            if (!sourceRequest.ValidUntil.HasValue || sourceRequest.ValidUntil.Value >= _dateTime.Now)
            {
                throw new InvalidOperationException("The referenced request has not expired.");
            }

            AddExpiredQuotationReferencesFromSource(request, sourceRequest);

            await _unitOfWork.Repository<Request>().UpdateAsync(request);
            await _unitOfWork.CommitAsync();
            await NotifyUpdatesAsync($"อ้างอิงใบเสนอราคาหมดอายุ {normalizedCode} ในเอกสาร {request.Code}");
        }

        public async Task UpdatePortalDocumentsAsync(int requestId, UpdatePortalDocumentsDto input, CancellationToken cancellationToken = default)
        {
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
                throw new InvalidOperationException("Attachments can only be updated on draft requests.");
            }

            if (input.Documents == null ||
                input.Documents.Count != request.Quotations.Count ||
                input.Documents.Select(document => document.Id).Distinct().Count() != input.Documents.Count ||
                input.Documents.Any(document => !request.Quotations.Any(quotation => quotation.Id == document.Id)))
            {
                throw new ArgumentException("Documents must contain every current attachment exactly once.");
            }

            if (input.Documents.Any(document => !Enum.IsDefined(typeof(DocumentType), document.DocumentTypeId)))
            {
                throw new ArgumentException("Every document must use a recognised document type.");
            }

            var quotationsById = request.Quotations.ToDictionary(quotation => quotation.Id);
            if (input.Documents.Any(document => quotationsById[document.Id].SourceQuotationId.HasValue
                && document.DocumentTypeId != (int)DocumentType.ExpiredQuotation))
            {
                throw new ArgumentException("Referenced quotations must remain Expired Quotation documents.");
            }

            for (var index = 0; index < input.Documents.Count; index++)
            {
                var update = input.Documents[index];
                var quotation = quotationsById[update.Id];
                quotation.DocumentTypeId = update.DocumentTypeId;
                quotation.SortOrder = index + 1;
            }

            await _unitOfWork.Repository<Request>().UpdateAsync(request);
            await _unitOfWork.CommitAsync();
            await NotifyUpdatesAsync($"แก้ไขลำดับไฟล์แนบในเอกสาร {request.Code}");
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

            if (request == null) throw new KeyNotFoundException($"Request {id} not found.");

            if (!request.ApprovalDocumentId.HasValue)
            {
                throw new InvalidOperationException("Request has no central approval document.");
            }

            var actingNId = _currentUserService.UserId;
            await _approvalService.ApproveAsync(request.ApprovalDocumentId.Value, actingNId, input.Comment, cancellationToken);
            await RefreshCentralMirrorAsync(request, actingNId, cancellationToken);

            await NotifyUpdatesAsync($"อนุมัติเอกสาร {request.Code}");
        }

        public async Task RejectPortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null) throw new KeyNotFoundException($"Request {id} not found.");

            if (!request.ApprovalDocumentId.HasValue)
            {
                throw new InvalidOperationException("Request has no central approval document.");
            }

            var actingNId = _currentUserService.UserId;
            await _approvalService.RejectAsync(
                request.ApprovalDocumentId.Value,
                actingNId,
                RequireActionComment(input.Comment, "reject"),
                cancellationToken);
            await RefreshCentralMirrorAsync(request, actingNId, cancellationToken);

            await NotifyUpdatesAsync($"ปฏิเสธเอกสาร {request.Code}");
        }

        public async Task ReturnPortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null) throw new KeyNotFoundException($"Request {id} not found.");
            if (!request.ApprovalDocumentId.HasValue) throw new InvalidOperationException("Request has no central approval document.");

            var actingNId = _currentUserService.UserId;
            await _approvalService.ReturnAsync(
                request.ApprovalDocumentId.Value,
                actingNId,
                RequireActionComment(input.Comment, "return"),
                input.ReturnToStepSequence,
                cancellationToken);
            await RefreshCentralMirrorAsync(request, actingNId, cancellationToken);

            await NotifyUpdatesAsync($"ส่งกลับเอกสาร {request.Code}");
        }

        public async Task CancelPortalRequestAsync(int id, PortalApprovalActionDto input, CancellationToken cancellationToken = default)
        {
            var request = await _unitOfWork.Repository<Request>().GetAll()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (request == null) throw new KeyNotFoundException($"Request {id} not found.");
            if (!request.ApprovalDocumentId.HasValue) throw new InvalidOperationException("Request has no central approval document.");

            var actingNId = _currentUserService.UserId;
            await _approvalService.CancelAsync(
                request.ApprovalDocumentId.Value,
                actingNId,
                RequireActionComment(input.Comment, "cancel"),
                cancellationToken);
            await RefreshCentralMirrorAsync(request, actingNId, cancellationToken);

            await NotifyUpdatesAsync($"ยกเลิกเอกสาร {request.Code}");
        }

        public async Task<ApprovalPreviewResult> GetRoutePreviewAsync(SavePortalRequestDto input, CancellationToken cancellationToken = default)
        {
            var actingNId = _currentUserService.UserId;
            var requesterOrgCode = await ResolveRequesterOrgCodeAsync(actingNId, cancellationToken);

            var docRequest = _approvalRequestFactory.Build(new ApprovalRequestContext(
                string.IsNullOrWhiteSpace(input.Title) ? "Preview Request" : input.Title,
                "PREVIEW",
                RequestId: null,
                requesterOrgCode,
                input.VendorCode,
                input.ValidFrom,
                input.ValidUntil,
                AttachmentCount: 0));

            return await _approvalService.PreviewRouteAsync(docRequest, actingNId, cancellationToken);
        }

        private async Task RefreshCentralMirrorAsync(
            Request request,
            string actingNId,
            CancellationToken cancellationToken)
        {
            var documentId = request.ApprovalDocumentId
                ?? throw new InvalidOperationException("Request has no central approval document.");
            var detail = await _approvalService.GetDocumentAsync(documentId, actingNId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Central approval document {documentId} was not found after the action completed.");

            request.ApprovalDocumentNumber = detail.Summary.DocumentNumber;
            request.Status = (int)detail.Summary.Status;
            request.CurrentStepSequence = detail.Summary.CurrentStepSequence;
            request.CurrentStepName = detail.Summary.CurrentStepName;
            request.StatusSyncedAt = _dateTime.Now;

            await _unitOfWork.Repository<Request>().UpdateAsync(request);
            await _unitOfWork.CommitAsync();
        }

        private static string RequireActionComment(string? comment, string action) =>
            string.IsNullOrWhiteSpace(comment)
                ? throw new ArgumentException($"A comment is required to {action} a request.", nameof(comment))
                : comment.Trim();

        private async Task<string> ResolveRequesterOrgCodeAsync(
            string actingNId,
            CancellationToken cancellationToken)
        {
            var employee = await _employeeDirectory.GetEmployeeDetailsAsync(actingNId, cancellationToken);
            if (employee is null || string.IsNullOrWhiteSpace(employee.DepartmentCode))
            {
                throw new InvalidOperationException(
                    $"Employee directory returned no organization for requester '{actingNId}'.");
            }

            return employee.DepartmentCode;
        }
    }
}