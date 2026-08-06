using QCS.Application.Abstractions;
using QCS.Domain.Enum;

namespace QCS.Api.IntegrationTests
{
    public sealed class FakeApprovalService : IApprovalService
    {
        private readonly Dictionary<Guid, ApprovalDocumentDetail> _documents = new();
        private readonly Dictionary<string, Guid> _documentsBySource = new(StringComparer.OrdinalIgnoreCase);

        public Exception? CreateException { get; set; }
        public Exception? ActionException { get; set; }
        public List<Guid> DeletedDraftIds { get; } = new();
        public int CreateCallCount { get; private set; }
        public List<Guid> SubmittedDocumentIds { get; } = new();

        public void Reset()
        {
            CreateException = null;
            ActionException = null;
            CreateCallCount = 0;
            _documents.Clear();
            _documentsBySource.Clear();
            DeletedDraftIds.Clear();
            SubmittedDocumentIds.Clear();
        }

        public void SeedDocument(
            string sourceNumber,
            Guid id,
            string actingNId,
            RequestStatus status = RequestStatus.Draft,
            ApprovalPermissions? permissions = null,
            int? currentStepSequence = 2,
            string? currentStepName = "Purchasing review",
            IReadOnlyList<ApprovalStepView>? steps = null)
        {
            _documents[id] = CreateDetail(
                id,
                actingNId,
                status,
                permissions,
                currentStepSequence,
                currentStepName,
                steps);
            _documentsBySource[sourceNumber] = id;
        }

        public Task<ApprovalPreviewResult> PreviewRouteAsync(
            ApprovalDocumentRequest request,
            string actingNId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApprovalPreviewResult(Array.Empty<ApprovalStepView>(), "QCS Workflow", "test"));

        public Task<ApprovalDocumentSummary> CreateDocumentAsync(
            ApprovalDocumentRequest request,
            string actingNId,
            CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            if (CreateException is not null)
            {
                throw CreateException;
            }

            var id = Guid.NewGuid();
            var detail = CreateDetail(id, actingNId, RequestStatus.InProcess);
            _documents[id] = detail;
            _documentsBySource[request.SourceNumber] = id;
            return Task.FromResult(detail.Summary);
        }

        public Task<ApprovalDocumentSummary> SubmitDocumentAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default)
        {
            SubmittedDocumentIds.Add(documentId);
            var detail = CreateDetail(documentId, actingNId, RequestStatus.InProcess);
            _documents[documentId] = detail;
            return Task.FromResult(detail.Summary);
        }

        public Task<ApprovalDocumentDetail?> GetDocumentAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_documents.GetValueOrDefault(documentId));

        public Task<ApprovalDocumentSummary?> FindBySourceAsync(
            string sourceNumber,
            CancellationToken cancellationToken = default)
        {
            var summary = _documentsBySource.TryGetValue(sourceNumber, out var id)
                ? _documents[id].Summary
                : null;
            return Task.FromResult(summary);
        }

        public Task<IReadOnlyList<Guid>> ListPendingDocumentIdsAsync(
            string actingNId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(_documents.Keys.ToArray());

        public Task<ApprovalDocumentDetail> RefreshAssigneesAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_documents[documentId]);

        public Task DeleteDraftAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default)
        {
            _documents.Remove(documentId);
            DeletedDraftIds.Add(documentId);
            return Task.CompletedTask;
        }

        public Task ApproveAsync(Guid documentId, string actingNId, string? comment, CancellationToken cancellationToken = default) =>
            TransitionAsync(documentId, RequestStatus.Completed);

        public Task RejectAsync(Guid documentId, string actingNId, string comment, CancellationToken cancellationToken = default) =>
            TransitionAsync(documentId, RequestStatus.Rejected);

        public Task ReturnAsync(Guid documentId, string actingNId, string comment, int? returnToSequence, CancellationToken cancellationToken = default) =>
            TransitionAsync(documentId, RequestStatus.Returned);

        public Task CancelAsync(Guid documentId, string actingNId, string comment, CancellationToken cancellationToken = default) =>
            TransitionAsync(documentId, RequestStatus.Cancelled);

        private Task TransitionAsync(Guid documentId, RequestStatus status)
        {
            if (ActionException is not null)
            {
                throw ActionException;
            }

            var detail = _documents[documentId];
            _documents[documentId] = detail with
            {
                Summary = detail.Summary with
                {
                    Status = status,
                    CurrentStepName = null,
                    CurrentStepSequence = null
                }
            };
            return Task.CompletedTask;
        }

        private static ApprovalDocumentDetail CreateDetail(
            Guid id,
            string actingNId,
            RequestStatus status,
            ApprovalPermissions? permissions = null,
            int? currentStepSequence = 2,
            string? currentStepName = "Purchasing review",
            IReadOnlyList<ApprovalStepView>? steps = null)
        {
            var summary = new ApprovalDocumentSummary(id, $"QC-{id:N}", status, currentStepName, currentStepSequence, null);
            permissions ??= new ApprovalPermissions(
                IsCreator: true,
                IsCurrentAssignee: false,
                CanSubmit: false,
                CanApprove: false,
                CanReject: false,
                CanReturn: false,
                CanCancel: true,
                AvailableActions: new[] { "Cancel" });

            return new ApprovalDocumentDetail(
                summary,
                steps ?? Array.Empty<ApprovalStepView>(),
                Array.Empty<ApprovalHistoryEntry>(),
                permissions);
        }
    }
}