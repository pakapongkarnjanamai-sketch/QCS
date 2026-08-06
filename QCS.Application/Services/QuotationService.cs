using Microsoft.EntityFrameworkCore;
using QCS.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QCS.Domain.DTOs;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Net;

namespace QCS.Application.Services
{
    public sealed class PdfServiceException : Exception
    {
        public int UpstreamStatusCode { get; }

        public PdfServiceException(string message, int upstreamStatusCode)
            : base(message)
        {
            UpstreamStatusCode = upstreamStatusCode;
        }
    }

    public interface IQuotationService
    {
        //IQueryable<RequestGridDto> GetGridQuery(string code = null);
        //Task<AttachmentResultDto?> GetAttachmentAsync(int id);
        Task<AttachmentResultDto> GenerateStampedPdfAsync(int requestId, CancellationToken cancellationToken = default);
        Task<AttachmentResultDto> GeneratePreviewMergedPdfAsync(MergeAndStampRequestDto pdfRequest, string fileName = "Preview", CancellationToken cancellationToken = default);

        /// <summary>
        /// Quotations that are still effective (Approved &amp; not past ValidUntil) for a single vendor.
        /// </summary>
        Task<List<QuotationDto>> GetEffectiveByVendorCodeAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Effective quotations with search / filter / sort / pagination applied.
        /// </summary>
        Task<PagedResult<QuotationDto>> GetEffectiveAsync(EffectiveQuotationQuery query, CancellationToken cancellationToken = default);
    }

    public class QuotationService : IQuotationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDateTime _dateTime;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<QuotationService> _logger;

        private readonly IApprovalService _approvalService;

        private const int PdfServiceTimeoutSeconds = 30;
        private static readonly TimeSpan[] RetryDelays =
        {
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2)
        };

        public QuotationService(
            IUnitOfWork unitOfWork,
            IDateTime dateTime,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<QuotationService> logger,
            IApprovalService approvalService)
        {
            _unitOfWork = unitOfWork;
            _dateTime = dateTime;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _approvalService = approvalService;
        }

        public async Task<AttachmentResultDto> GenerateStampedPdfAsync(int requestId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating approved quotation PDF for requestId={RequestId}", requestId);

            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

            if (request == null) throw new KeyNotFoundException("Request not found");

            List<StepDto> stampSteps = new();

            if (request.ApprovalDocumentId.HasValue)
            {
                var detail = await _approvalService.GetDocumentAsync(
                    request.ApprovalDocumentId.Value,
                    request.CreatedBy ?? "SYSTEM",
                    cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Central approval document {request.ApprovalDocumentId.Value} was not found.");

                foreach (var step in detail.Steps)
                {
                    foreach (var assignee in step.Assignees.Where(assignee => assignee.ActedAt.HasValue))
                    {
                        stampSteps.Add(new StepDto
                        {
                            StepName = step.StepName,
                            Approver = assignee.EmployeeName ?? assignee.Username,
                            ApprovalDate = assignee.ActedAt!.Value
                        });
                    }
                }

                if (stampSteps.Count == 0)
                {
                    throw new InvalidOperationException(
                        "The central Approval Service returned no completed approval actions to stamp.");
                }
            }
            else
            {
                stampSteps = request.ApprovalSteps
                    .Where(s => s.Status == (int)LegacyApprovalStepStatus.Approved && s.ActionDate.HasValue)
                    .OrderBy(s => s.Sequence)
                    .Select(s => new StepDto
                    {
                        StepName = s.StepName,
                        Approver = s.ApproverName ?? s.ApproverNId ?? "Unknown",
                        ApprovalDate = s.ActionDate!.Value
                    }).ToList();

                if (stampSteps.Count == 0)
                {
                    throw new InvalidOperationException(
                        "The legacy request has no completed approval actions to stamp.");
                }
            }

            var pdfRequest = new MergeAndStampRequestDto
            {
                DocumentName = request.Code +"_"+ request.Title,
                ReferenceCode = request.Code,
                PdfFiles = request.Quotations
                    .Where(q => q.AttachmentFile != null)
                    .Select(q => new PdfFileDto
                    {
                        Name = q.FileName,
                        DocumentTypeId = q.DocumentTypeId,
                        ContentType = q.ContentType ?? "application/pdf",
                        Data = q.AttachmentFile.Data,
                        Length = q.FileSize
                    }).ToList(),
                ApprovalData = new ApprovalDataDto
                {
                    Name = request.VendorName,
                    Step = stampSteps
                },
                DrawSetting = new DrawSettingDto
                {
                    Color = "#000000",
                    FontSize = 8,
                    Margin = 20,
                    AlignmentStamp = 2
                }
            };

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var payloadJson = JsonSerializer.Serialize(pdfRequest, jsonOptions);
            var fileBytes = await CallMergeAndStampAsync(payloadJson, requestId, cancellationToken);

            return new AttachmentResultDto
            {
                Data = fileBytes,
                ContentType = "application/pdf",
                FileName = $"Approved_{request.Code}.pdf"
            };
        }

        public async Task<AttachmentResultDto> GeneratePreviewMergedPdfAsync(MergeAndStampRequestDto pdfRequest, string fileName = "Preview", CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating preview merged PDF fileName={FileName}", fileName);

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var payloadJson = JsonSerializer.Serialize(pdfRequest, jsonOptions);
            var fileBytes = await CallMergeAndStampAsync(payloadJson, null, cancellationToken);

            return new AttachmentResultDto
            {
                Data = fileBytes,
                ContentType = "application/pdf",
                FileName = $"{fileName}.pdf"
            };
        }

        private async Task<byte[]> CallMergeAndStampAsync(string payloadJson, int? requestId, CancellationToken cancellationToken)
        {
            var requestUrl = BuildPdfMergeStampUrl();
            Exception? lastException = null;
            var startedAt = Stopwatch.StartNew();

            for (int attempt = 0; attempt <= RetryDelays.Length; attempt++)
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(PdfServiceTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                using var jsonContent = new StringContent(payloadJson, Encoding.UTF8, "application/json");

                try
                {
                    var response = await _httpClient.PostAsync(requestUrl, jsonContent, linkedCts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsByteArrayAsync();
                        _logger.LogInformation(
                            "PDF merge-stamp success requestId={RequestId} attempt={Attempt} elapsedMs={ElapsedMs}",
                            requestId,
                            attempt + 1,
                            startedAt.ElapsedMilliseconds);
                        return result;
                    }

                    var errorMsg = await response.Content.ReadAsStringAsync();
                    var statusCode = (int)response.StatusCode;
                    var ex = new PdfServiceException($"PDF Service Error ({response.StatusCode}): {errorMsg}", statusCode);
                    lastException = ex;

                    _logger.LogWarning(
                        "PDF merge-stamp failed requestId={RequestId} attempt={Attempt} statusCode={StatusCode} transient={IsTransient}",
                        requestId,
                        attempt + 1,
                        statusCode,
                        IsTransientStatusCode(response.StatusCode));

                    if (!IsTransientStatusCode(response.StatusCode) || attempt == RetryDelays.Length)
                    {
                        throw ex;
                    }
                }
                catch (TaskCanceledException ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation(
                            "PDF merge-stamp cancelled by caller requestId={RequestId} attempt={Attempt}",
                            requestId,
                            attempt + 1);
                        throw;
                    }

                    lastException = ex;
                    _logger.LogWarning(
                        "PDF merge-stamp timeout requestId={RequestId} attempt={Attempt} timeoutSeconds={TimeoutSeconds}",
                        requestId,
                        attempt + 1,
                        PdfServiceTimeoutSeconds);

                    if (attempt == RetryDelays.Length)
                    {
                        throw new PdfServiceException($"PDF Service timeout after {PdfServiceTimeoutSeconds}s", (int)HttpStatusCode.GatewayTimeout);
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    _logger.LogWarning(
                        ex,
                        "PDF merge-stamp connectivity error requestId={RequestId} attempt={Attempt}",
                        requestId,
                        attempt + 1);

                    if (attempt == RetryDelays.Length)
                    {
                        throw new PdfServiceException("Cannot connect to PDF service", (int)HttpStatusCode.BadGateway);
                    }
                }

                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }

            _logger.LogError(
                lastException,
                "PDF merge-stamp exhausted retries requestId={RequestId} elapsedMs={ElapsedMs}",
                requestId,
                startedAt.ElapsedMilliseconds);

            throw new PdfServiceException($"PDF Service request failed: {lastException?.Message}", (int)HttpStatusCode.BadGateway);
        }

        private static bool IsTransientStatusCode(HttpStatusCode statusCode)
        {
            var numericStatus = (int)statusCode;
            return statusCode == HttpStatusCode.RequestTimeout
                || statusCode == HttpStatusCode.TooManyRequests
                || numericStatus >= 500;
        }

        private string BuildPdfMergeStampUrl()
        {
            var pdfServiceUrl = _configuration["ExternalServices:PdfServiceUrl"];
            if (string.IsNullOrWhiteSpace(pdfServiceUrl))
            {
                throw new InvalidOperationException("Missing configuration: ExternalServices:PdfServiceUrl");
            }

            return $"{pdfServiceUrl.TrimEnd('/')}/api/Pdf/merge-stamp";
        }

        // =================================================================================================
        // Effective Quotation queries (feed the front-end table)
        // =================================================================================================

        private const int MaxPageSize = 200;
        private const int DefaultPageSize = 20;

        /// <summary>
        /// Projects a <see cref="Request"/> header into a <see cref="QuotationDto"/>.
        /// RequesterName comes from the first approval step (the purchaser); RequesterNId is the creator.
        /// </summary>
        private static readonly Expression<Func<Request, QuotationDto>> QuotationProjection = r => new QuotationDto
        {
            Id = r.Id,
            Code = r.Code ?? string.Empty,
            Title = r.Title ?? string.Empty,
            VendorCode = r.VendorCode ?? string.Empty,
            VendorName = r.VendorName ?? string.Empty,
            RequestDate = r.RequestDate,
            CurrentStepId = r.CurrentStepSequence ?? 0,
            RequesterName = r.ApprovalSteps
                .Where(s => s.Sequence == 1)
                .Select(s => s.ApproverName)
                .FirstOrDefault() ?? string.Empty,
            RequesterNId = r.CreatedBy ?? string.Empty,
            Remark = r.Remark ?? string.Empty,
            ValidFrom = r.ValidFrom ?? default,
            ValidUntil = r.ValidUntil ?? default
        };

        /// <summary>
        /// Base query for "effective" quotations: approved documents whose validity has not yet lapsed.
        /// Matches the existing IntegrationController semantics (Status = Completed &amp; ValidUntil &gt;= now).
        /// </summary>
        private IQueryable<Request> GetEffectiveRequestsQuery()
        {
            var now = _dateTime.Now;
            return _unitOfWork.Repository<Request>().GetAll()
                .AsNoTracking()
                .Where(r => r.Status == (int)RequestStatus.Completed
                         && r.ValidUntil != null
                         && r.ValidUntil >= now);
        }

        public async Task<List<QuotationDto>> GetEffectiveByVendorCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return new List<QuotationDto>();
            }

            var vendorCode = code.Trim();

            return await GetEffectiveRequestsQuery()
                .Where(r => r.VendorCode == vendorCode)
                .OrderByDescending(r => r.RequestDate)
                .ThenByDescending(r => r.Id)
                .Select(QuotationProjection)
                .ToListAsync(cancellationToken);
        }

        public async Task<PagedResult<QuotationDto>> GetEffectiveAsync(EffectiveQuotationQuery query, CancellationToken cancellationToken = default)
        {
            query ??= new EffectiveQuotationQuery();

            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

            var filtered = ApplyFilters(GetEffectiveRequestsQuery(), query);

            var totalCount = await filtered.CountAsync(cancellationToken);

            var sorted = ApplySort(filtered, query.SortBy, query.SortDescending);

            var items = await sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(QuotationProjection)
                .ToListAsync(cancellationToken);

            return new PagedResult<QuotationDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        private static IQueryable<Request> ApplyFilters(IQueryable<Request> source, EffectiveQuotationQuery query)
        {
            if (!string.IsNullOrWhiteSpace(query.VendorCode))
            {
                var vendorCode = query.VendorCode.Trim();
                source = source.Where(r => r.VendorCode == vendorCode);
            }

            if (!string.IsNullOrWhiteSpace(query.VendorName))
            {
                var vendorName = query.VendorName.Trim();
                source = source.Where(r => r.VendorName != null && r.VendorName.Contains(vendorName));
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim();
                source = source.Where(r =>
                    (r.Code != null && r.Code.Contains(keyword)) ||
                    (r.Title != null && r.Title.Contains(keyword)) ||
                    (r.VendorName != null && r.VendorName.Contains(keyword)) ||
                    (r.VendorCode != null && r.VendorCode.Contains(keyword)));
            }

            if (query.RequestDateFrom.HasValue)
            {
                var from = query.RequestDateFrom.Value.Date;
                source = source.Where(r => r.RequestDate >= from);
            }

            if (query.RequestDateTo.HasValue)
            {
                // Inclusive end date: include the whole "to" day.
                var toExclusive = query.RequestDateTo.Value.Date.AddDays(1);
                source = source.Where(r => r.RequestDate < toExclusive);
            }

            if (query.CurrentStepId.HasValue)
            {
                var step = query.CurrentStepId.Value;
                source = source.Where(r => r.CurrentStepSequence == step);
            }

            return source;
        }

        private static IQueryable<Request> ApplySort(IQueryable<Request> source, string? sortBy, bool descending)
        {
            // Id tie-breaker keeps pagination stable when the sort key has duplicates.
            return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "code" => descending
                    ? source.OrderByDescending(r => r.Code).ThenByDescending(r => r.Id)
                    : source.OrderBy(r => r.Code).ThenBy(r => r.Id),
                "title" => descending
                    ? source.OrderByDescending(r => r.Title).ThenByDescending(r => r.Id)
                    : source.OrderBy(r => r.Title).ThenBy(r => r.Id),
                "vendorcode" => descending
                    ? source.OrderByDescending(r => r.VendorCode).ThenByDescending(r => r.Id)
                    : source.OrderBy(r => r.VendorCode).ThenBy(r => r.Id),
                "vendorname" => descending
                    ? source.OrderByDescending(r => r.VendorName).ThenByDescending(r => r.Id)
                    : source.OrderBy(r => r.VendorName).ThenBy(r => r.Id),
                "requestdate" => descending
                    ? source.OrderByDescending(r => r.RequestDate).ThenByDescending(r => r.Id)
                    : source.OrderBy(r => r.RequestDate).ThenBy(r => r.Id),
                "validfrom" => descending
                    ? source.OrderByDescending(r => r.ValidFrom).ThenByDescending(r => r.Id)
                    : source.OrderBy(r => r.ValidFrom).ThenBy(r => r.Id),
                "validuntil" => descending
                    ? source.OrderByDescending(r => r.ValidUntil).ThenByDescending(r => r.Id)
                    : source.OrderBy(r => r.ValidUntil).ThenBy(r => r.Id),
                "currentstepid" => descending
                    ? source.OrderByDescending(r => r.CurrentStepSequence).ThenByDescending(r => r.Id)
                    : source.OrderBy(r => r.CurrentStepSequence).ThenBy(r => r.Id),
                // Default: most recently requested first.
                _ => source.OrderByDescending(r => r.RequestDate).ThenByDescending(r => r.Id),
            };
        }

    }
}