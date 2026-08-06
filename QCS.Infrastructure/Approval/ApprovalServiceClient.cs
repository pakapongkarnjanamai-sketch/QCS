using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QCS.Application.Abstractions;
using QCS.Domain.Enum;

namespace QCS.Infrastructure.Approval
{
    public sealed class ApprovalServiceClient : IApprovalService
    {
        internal const string WorkflowHttpClientName = "ApprovalWorkflow";
        internal const string ForwardedUserHeader = "X-Gpcs-Authenticated-User";
        internal const string ForwardedAuthTypeHeader = "X-Gpcs-Authentication-Type";
        internal const string ForwardedSecretHeader = "X-Gpcs-Auth-Secret";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<ApprovalServiceOptions> _options;
        private readonly ILogger<ApprovalServiceClient> _logger;

        public ApprovalServiceClient(
            HttpClient httpClient,
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<ApprovalServiceOptions> options,
            ILogger<ApprovalServiceClient> logger)
        {
            _httpClient = httpClient;
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
        }

        private ApprovalServiceOptions Options => _options.CurrentValue;

        public async Task<ApprovalPreviewResult> PreviewRouteAsync(
            ApprovalDocumentRequest request,
            string actingNId,
            CancellationToken cancellationToken = default)
        {
            var payload = new ResolveWorkflowRequestDto(
                Options.DocumentTypeCode,
                actingNId,
                request.DocumentOrgCodes,
                request.ConditionalData);

            var workflowClient = _httpClientFactory.CreateClient(WorkflowHttpClientName);
            using var response = await workflowClient.PostAsJsonAsync(
                "api/workflows/resolve-preview", payload, Json, cancellationToken);

            await EnsureSuccessAsync(response, "api/workflows/resolve-preview", cancellationToken);

            var workflow = await ReadEnvelopeAsync<ResolvedWorkflowDto>(
                response,
                "api/workflows/resolve-preview",
                cancellationToken);

            return new ApprovalPreviewResult(
                MapResolvedSteps(workflow?.Steps),
                workflow?.Name,
                workflow?.Version?.ToString(CultureInfo.InvariantCulture));
        }

        public async Task<ApprovalDocumentSummary> CreateDocumentAsync(
            ApprovalDocumentRequest request,
            string actingNId,
            CancellationToken cancellationToken = default)
        {
            var payload = new CreateDocumentCommandDto(
                request.Title,
                DocumentType(),
                Source(request),
                request.IsUrgent,
                request.RequesterOrgCode,
                request.DocumentOrgCodes,
                request.ConditionalData,
                request.EffectiveDate,
                DocumentNumber: null,
                NId: actingNId);

            var created = await PostAsync<CreateDocumentCommandDto, DocumentDto>(
                "api/documents", payload, actingNId, cancellationToken);

            if (created is null || created.Id == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "The Approval Service accepted the request but returned no document id.");
            }

            return await SubmitDocumentAsync(created.Id, actingNId, cancellationToken);
        }

        public async Task<ApprovalDocumentSummary> SubmitDocumentAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default)
        {
            await PostAsync<ActionRequestDto, DocumentDto>(
                $"api/documents/{documentId}/submit",
                new ActionRequestDto(null, actingNId),
                actingNId,
                cancellationToken);

            var detail = await GetDocumentAsync(documentId, actingNId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"The Approval Service accepted document {documentId} but it could not be read back.");

            return detail.Summary;
        }

        public async Task<ApprovalDocumentDetail?> GetDocumentAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default)
        {
            var document = await GetAsync<DocumentDto>(
                $"api/documents/{documentId}?nid={Uri.EscapeDataString(actingNId)}",
                actingNId,
                cancellationToken);

            if (document is null) return null;

            var p = document.Permissions;

            return new ApprovalDocumentDetail(
                MapSummary(document),
                MapSteps(document.Workflow?.Steps),
                document.History?.Select(h => new ApprovalHistoryEntry(
                    h.Action ?? string.Empty, h.StepName, h.ActorName,
                    h.FromStatus, h.ToStatus, h.Reason, h.CreatedAt)).ToArray() ?? Array.Empty<ApprovalHistoryEntry>(),
                p is null
                    ? ApprovalPermissions.None
                    : new ApprovalPermissions(
                        p.IsCreator, p.IsCurrentAssignee, p.CanSubmit, p.CanApprove,
                        p.CanReject, p.CanReturn, p.CanCancel,
                        p.AvailableActions?
                            .Select(a => a.ActionType)
                            .Where(a => !string.IsNullOrWhiteSpace(a))
                            .Select(a => a!)
                            .ToArray() ?? Array.Empty<string>()));
        }

        public async Task<ApprovalDocumentSummary?> FindBySourceAsync(
            string sourceNumber,
            CancellationToken cancellationToken = default)
        {
            var url = $"api/documents/by-source?system={Uri.EscapeDataString(Options.SourceSystem)}"
                      + $"&number={Uri.EscapeDataString(sourceNumber)}";

            var document = await GetAsync<DocumentDto>(url, actingNId: null, cancellationToken);
            return document is null ? null : MapSummary(document);
        }

        public async Task<IReadOnlyList<Guid>> ListPendingDocumentIdsAsync(
            string actingNId,
            CancellationToken cancellationToken = default)
        {
            const int pageSize = 200;
            var documentIds = new List<Guid>();
            var pageNumber = 1;

            while (true)
            {
                var url = $"api/documents?mine=true&page={pageNumber}&pageSize={pageSize}"
                          + $"&documentType={Uri.EscapeDataString(Options.DocumentTypeCode)}"
                          + $"&nid={Uri.EscapeDataString(actingNId)}";

                var page = await GetAsync<PagedDto<DocumentDto>>(url, actingNId, cancellationToken);
                var items = page?.Items ?? Array.Empty<DocumentDto>();
                documentIds.AddRange(items.Select(document => document.Id));

                if (items.Count < pageSize ||
                    page?.TotalCount is { } totalCount && documentIds.Count >= totalCount)
                {
                    break;
                }

                pageNumber++;
            }

            return documentIds;
        }

        public async Task<ApprovalDocumentDetail> RefreshAssigneesAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/documents/{documentId}/refresh-assignees");
            AddForwardedUser(request, actingNId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, request.RequestUri!.ToString(), cancellationToken);

            return await GetDocumentAsync(documentId, actingNId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Approval document {documentId} disappeared after its assignees were refreshed.");
        }

        public async Task DeleteDraftAsync(
            Guid documentId,
            string actingNId,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/documents/{documentId}");
            AddForwardedUser(request, actingNId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return;
            await EnsureSuccessAsync(response, $"api/documents/{documentId}", cancellationToken);
        }

        public Task ApproveAsync(Guid documentId, string actingNId, string? comment, CancellationToken cancellationToken = default) =>
            PostAsync<ActionRequestDto, DocumentDto>(
                $"api/documents/{documentId}/approve", new ActionRequestDto(comment, actingNId), actingNId, cancellationToken);

        public Task RejectAsync(Guid documentId, string actingNId, string comment, CancellationToken cancellationToken = default) =>
            PostAsync<ActionRequestDto, DocumentDto>(
                $"api/documents/{documentId}/reject", new ActionRequestDto(RequireComment(comment, "rejecting"), actingNId), actingNId, cancellationToken);

        public Task ReturnAsync(Guid documentId, string actingNId, string comment, int? returnToSequence, CancellationToken cancellationToken = default) =>
            PostAsync<ReturnActionRequestDto, DocumentDto>(
                $"api/documents/{documentId}/return",
                new ReturnActionRequestDto(RequireComment(comment, "returning"), returnToSequence, actingNId),
                actingNId,
                cancellationToken);

        public Task CancelAsync(Guid documentId, string actingNId, string comment, CancellationToken cancellationToken = default) =>
            PostAsync<ActionRequestDto, DocumentDto>(
                $"api/documents/{documentId}/cancel", new ActionRequestDto(RequireComment(comment, "cancelling"), actingNId), actingNId, cancellationToken);

        // --- Helper Methods ---------------------------------------------------

        private DocumentTypeInputDto DocumentType() =>
            new(Options.DocumentTypeCode, Options.DocumentTypeName);

        private SourceInputDto Source(ApprovalDocumentRequest request) =>
            new(Options.SourceSystem, request.SourceNumber, request.SourceUrl);

        private static ApprovalDocumentSummary MapSummary(DocumentDto d) =>
            new(d.Id,
                d.DocumentNumber,
                ParseStatus(d.Status),
                d.CurrentStepName ?? d.Workflow?.Steps?
                    .FirstOrDefault(s => string.Equals(s.Status, "InProgress", StringComparison.OrdinalIgnoreCase))?.StepName,
                d.CurrentStepSequence ?? d.Workflow?.CurrentStepSequence,
                d.CompletedAt);

        private static IReadOnlyList<ApprovalStepView> MapSteps(IReadOnlyList<WorkflowStepDto>? steps) =>
            steps?.Select(s => new ApprovalStepView(
                s.SequenceNo,
                s.StepName ?? string.Empty,
                s.DisplayStatus ?? s.Status,
                s.IsFinalStep,
                s.Assignees?.Select(a => new ApprovalAssigneeView(
                    a.Username ?? string.Empty,
                    a.EmployeeName,
                    a.DisplayStatus ?? a.Status,
                    a.ActedAt,
                    a.Comment)).ToArray() ?? Array.Empty<ApprovalAssigneeView>())).ToArray() ?? Array.Empty<ApprovalStepView>();

        private static IReadOnlyList<ApprovalStepView> MapResolvedSteps(
            IReadOnlyList<ResolvedWorkflowStepDto>? steps) =>
            steps?.Select(s => new ApprovalStepView(
                s.Sequence,
                s.StepName ?? string.Empty,
                Status: null,
                s.IsFinalStep,
                s.Assignees?.Select(a => new ApprovalAssigneeView(
                    a.Username ?? string.Empty,
                    a.EmployeeName,
                    DisplayStatus: null,
                    ActedAt: null,
                    Comment: null)).ToArray() ?? Array.Empty<ApprovalAssigneeView>())).ToArray() ?? Array.Empty<ApprovalStepView>();

        private static RequestStatus ParseStatus(string? statusStr)
        {
            if (string.IsNullOrWhiteSpace(statusStr))
            {
                throw new InvalidOperationException("The Approval Service returned a document with no status.");
            }

            return statusStr.Trim() switch
            {
                "Draft" => RequestStatus.Draft,
                "InProcess" => RequestStatus.InProcess,
                "Returned" => RequestStatus.Returned,
                "Rejected" => RequestStatus.Rejected,
                "WaitingEffective" => RequestStatus.WaitingEffective,
                "Completed" => RequestStatus.Completed,
                "Cancelled" => RequestStatus.Cancelled,
                _ => throw new InvalidOperationException($"Unrecognised GPCS document status: '{statusStr}'.")
            };
        }

        private async Task<TResult?> GetAsync<TResult>(
            string relativeUrl,
            string? actingNId,
            CancellationToken cancellationToken)
            where TResult : class
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            AddForwardedUser(request, actingNId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;

            await EnsureSuccessAsync(response, relativeUrl, cancellationToken);

            return await ReadEnvelopeAsync<TResult>(response, relativeUrl, cancellationToken);
        }

        private async Task<TResult?> PostAsync<TInput, TResult>(
            string relativeUrl,
            TInput payload,
            string? actingNId,
            CancellationToken cancellationToken)
            where TResult : class
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl)
            {
                Content = JsonContent.Create(payload, options: Json)
            };
            AddForwardedUser(request, actingNId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, relativeUrl, cancellationToken);

            return await ReadEnvelopeAsync<TResult>(response, relativeUrl, cancellationToken);
        }

        private static async Task<TResult?> ReadEnvelopeAsync<TResult>(
            HttpResponseMessage response,
            string endpoint,
            CancellationToken cancellationToken)
            where TResult : class
        {
            var envelope = await response.Content
                .ReadFromJsonAsync<ApprovalEnvelope<TResult>>(Json, cancellationToken);

            if (envelope is null)
            {
                throw new InvalidOperationException(
                    $"Approval Service endpoint {endpoint} returned an invalid response envelope.");
            }
            if (!envelope.Success)
            {
                throw new InvalidOperationException(
                    $"Approval Service endpoint {endpoint} reported failure: {envelope.Message ?? "no message"}.");
            }

            return envelope.Data;
        }

        private void AddForwardedUser(HttpRequestMessage request, string? actingNId)
        {
            if (!string.IsNullOrWhiteSpace(actingNId))
            {
                request.Headers.Add(ForwardedUserHeader, actingNId);
            }
            request.Headers.Add(ForwardedAuthTypeHeader, "QCS");
            request.Headers.Add(ForwardedSecretHeader, Options.ForwardedUserSecret);
        }

        private async Task EnsureSuccessAsync(
            HttpResponseMessage response,
            string endpoint,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Approval Service endpoint {Endpoint} returned HTTP {StatusCode}: {Body}",
                endpoint,
                (int)response.StatusCode,
                body);

            throw new InvalidOperationException(
                $"Approval Service call to {endpoint} failed with status {(int)response.StatusCode}.");
        }

        private static string RequireComment(string? comment, string actionName)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                throw new ArgumentException($"A comment is required when {actionName} a document.", nameof(comment));
            }
            return comment.Trim();
        }

        // --- Internal Wire DTOs ------------------------------------------------

        private sealed record ApprovalEnvelope<T>(T? Data, bool Success, string? Message);

        private sealed record DocumentTypeInputDto(string Code, string Name);
        private sealed record SourceInputDto(string System, string Number, string Url);

        private sealed record ResolveWorkflowRequestDto(
            [property: JsonPropertyName("documentType")] string DocumentTypeCode,
            [property: JsonPropertyName("requesterUsername")] string RequesterNId,
            IReadOnlyList<string> DocumentOrgCodes,
            IReadOnlyDictionary<string, string?> ConditionalData);

        private sealed record ResolvedWorkflowAssigneeDto(
            string Username,
            string? EmployeeName);

        private sealed record ResolvedWorkflowStepDto(
            int Sequence,
            string StepName,
            bool IsFinalStep,
            IReadOnlyList<ResolvedWorkflowAssigneeDto>? Assignees);

        private sealed record ResolvedWorkflowDto(
            string Name,
            int? Version,
            IReadOnlyList<ResolvedWorkflowStepDto>? Steps);

        private sealed record CreateDocumentCommandDto(
            string Title,
            DocumentTypeInputDto DocumentType,
            SourceInputDto Source,
            bool IsUrgent,
            string RequesterOrgCode,
            IReadOnlyList<string> DocumentOrgCodes,
            IReadOnlyDictionary<string, string?> ConditionalData,
            DateTime? EffectiveDate,
            string? DocumentNumber,
            string NId);

        private sealed record ActionRequestDto(string? Comment, string NId);
        private sealed record ReturnActionRequestDto(string Comment, int? ReturnToStepSequence, string NId);

        private sealed record WorkflowAssigneeDto(
            string Username,
            string? EmployeeName,
            string? Status,
            string? DisplayStatus,
            DateTime? ActedAt,
            string? Comment);

        private sealed record WorkflowStepDto(
            int SequenceNo,
            string StepName,
            string? Status,
            string? DisplayStatus,
            bool IsFinalStep,
            IReadOnlyList<WorkflowAssigneeDto>? Assignees);

        private sealed record DocumentWorkflowDto(
            int? CurrentStepSequence,
            IReadOnlyList<WorkflowStepDto>? Steps);

        private sealed record ActionMetadataDto(string ActionType);

        private sealed record DocumentPermissionsDto(
            bool IsCreator,
            bool IsCurrentAssignee,
            bool CanSubmit,
            bool CanApprove,
            bool CanReject,
            bool CanReturn,
            bool CanCancel,
            IReadOnlyList<ActionMetadataDto>? AvailableActions);

        private sealed record DocumentHistoryDto(
            string Action,
            string? StepName,
            string? ActorName,
            string? FromStatus,
            string? ToStatus,
            string? Reason,
            DateTime CreatedAt);

        private sealed record DocumentDto(
            Guid Id,
            string? DocumentNumber,
            string? Status,
            string? CurrentStepName,
            int? CurrentStepSequence,
            DateTime? CompletedAt,
            DocumentWorkflowDto? Workflow,
            DocumentPermissionsDto? Permissions,
            IReadOnlyList<DocumentHistoryDto>? History);

        private sealed record PagedDto<T>(IReadOnlyList<T>? Items, int? TotalCount);
    }
}
