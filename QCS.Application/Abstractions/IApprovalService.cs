using QCS.Domain.Enum;

namespace QCS.Application.Abstractions
{
    public sealed record ApprovalDocumentRequest(
        string Title,
        string SourceNumber,
        string SourceUrl,
        bool IsUrgent,
        string RequesterOrgCode,
        IReadOnlyList<string> DocumentOrgCodes,
        IReadOnlyDictionary<string, object?> ConditionalData,
        DateTime? EffectiveDate = null
    );

    public sealed record ApprovalRequestContext(
        string Title,
        string SourceNumber,
        int? RequestId,
        string RequesterOrgCode,
        string? VendorCode,
        DateTime? ValidFrom,
        DateTime? ValidUntil,
        int AttachmentCount);

    public interface IApprovalRequestFactory
    {
        ApprovalDocumentRequest Build(ApprovalRequestContext context);
    }

    public sealed record ApprovalDocumentSummary(
        Guid Id,
        string? DocumentNumber,
        RequestStatus Status,
        string? CurrentStepName,
        int? CurrentStepSequence,
        DateTime? CompletedAt
    );

    public sealed record ApprovalAssigneeView(
        string Username,
        string? EmployeeName,
        string? DisplayStatus,
        DateTime? ActedAt,
        string? Comment
    );

    public sealed record ApprovalStepView(
        int SequenceNo,
        string StepName,
        string? Status,
        bool IsFinalStep,
        IReadOnlyList<ApprovalAssigneeView> Assignees
    );

    public sealed record ApprovalHistoryEntry(
        string Action,
        string? StepName,
        string? ActorName,
        string? FromStatus,
        string? ToStatus,
        string? Reason,
        DateTime CreatedAt
    );

    public sealed record ApprovalPermissions(
        bool IsCreator,
        bool IsCurrentAssignee,
        bool CanSubmit,
        bool CanApprove,
        bool CanReject,
        bool CanReturn,
        bool CanCancel,
        IReadOnlyList<string> AvailableActions
    )
    {
        public static ApprovalPermissions None { get; } = new(false, false, false, false, false, false, false, Array.Empty<string>());
    }

    public sealed record ApprovalDocumentDetail(
        ApprovalDocumentSummary Summary,
        IReadOnlyList<ApprovalStepView> Steps,
        IReadOnlyList<ApprovalHistoryEntry> History,
        ApprovalPermissions Permissions
    );

    public sealed record ApprovalPreviewResult(
        IReadOnlyList<ApprovalStepView> Steps,
        string? WorkflowName,
        string? WorkflowVersion
    );

    public interface IApprovalService
    {
        Task<ApprovalPreviewResult> PreviewRouteAsync(
            ApprovalDocumentRequest request,
            string actingNId,
            CancellationToken cancellationToken = default);

        Task<ApprovalDocumentSummary> CreateDocumentAsync(
            ApprovalDocumentRequest request,
            string actingNId,
            CancellationToken cancellationToken = default);

        Task<ApprovalDocumentSummary> SubmitDocumentAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default);

        Task<ApprovalDocumentDetail?> GetDocumentAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default);

        Task<ApprovalDocumentSummary?> FindBySourceAsync(
            string sourceNumber,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Guid>> ListPendingDocumentIdsAsync(
            string actingNId,
            CancellationToken cancellationToken = default);

        Task<ApprovalDocumentDetail> RefreshAssigneesAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default);

        Task DeleteDraftAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default);

        Task ApproveAsync(Guid documentId, string actingNId, string? comment, CancellationToken cancellationToken = default);

        Task RejectAsync(Guid documentId, string actingNId, string comment, CancellationToken cancellationToken = default);

        Task ReturnAsync(Guid documentId, string actingNId, string comment, int? returnToSequence, CancellationToken cancellationToken = default);

        Task CancelAsync(Guid documentId, string actingNId, string comment, CancellationToken cancellationToken = default);
    }
}
