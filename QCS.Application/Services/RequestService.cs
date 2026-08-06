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
        Task<AttachmentResultDto?> GetAttachmentAsync(int id);
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
            IEmployeeDirectory employeeDirectory)
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
                .Include(r => r.Quotations)
                .Include(r => r.ApprovalSteps)
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
                ApprovalDocumentId = request.ApprovalDocumentId,
                ApprovalDocumentNumber = request.ApprovalDocumentNumber,
                CurrentStepSequence = request.CurrentStepSequence,
                CurrentStepId = request.CurrentStepSequence ?? 0,
                CurrentStepName = centralDetail?.Summary.CurrentStepName ?? request.CurrentStepName,
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
                        VendorCode = input.VendorCode ?? string.Empty,
                        VendorName = input.VendorName ?? string.Empty,
                        SourceSystem = input.SourceSystem,
                        SourceCode = input.SourceCode,
                        ValidFrom = input.ValidFrom,
                        ValidUntil = input.ValidUntil,
                        Remark = input.Remark
                    };

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

            if (request == null) throw new KeyNotFoundException($"Request {id} not found.");
            if (!string.Equals(request.CreatedBy, _currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("You do not have permission to submit this request.");
            if (request.Status != (int)RequestStatus.Draft && request.Status != (int)RequestStatus.Returned)
                throw new InvalidOperationException("Only draft or returned requests can be submitted.");

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(request.Title)) errors.Add("Title is required.");
            if (string.IsNullOrWhiteSpace(request.VendorCode)) errors.Add("VendorCode is required.");
            if (string.IsNullOrWhiteSpace(request.VendorName)) errors.Add("VendorName is required.");
            if (request.ValidFrom == null) errors.Add("ValidFrom date is required.");
            if (request.ValidUntil == null) errors.Add("ValidUntil date is required.");
            if (request.ValidFrom != null && request.ValidUntil != null && request.ValidFrom > request.ValidUntil)
                errors.Add("ValidFrom date cannot be after ValidUntil date.");
            if (!request.Quotations.Any(q => q.DocumentTypeId == (int)DocumentType.OriginalQuotation))
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