using Microsoft.EntityFrameworkCore;
using QCS.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QCS.Domain.DTOs;
using QCS.Domain.Models;
using System.Diagnostics;
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
    }

    public class QuotationService : IQuotationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDateTime _dateTime;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<QuotationService> _logger;

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
            ILogger<QuotationService> logger)
        {
            _unitOfWork = unitOfWork;
            _dateTime = dateTime;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AttachmentResultDto> GenerateStampedPdfAsync(int requestId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating approved quotation PDF for requestId={RequestId}", requestId);

            var request = await _unitOfWork.Repository<Request>().GetAll()
                .Include(r => r.Quotations).ThenInclude(q => q.AttachmentFile)
                .Include(r => r.ApprovalSteps)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

            if (request == null) throw new KeyNotFoundException("Request not found");

            var fallbackApprovalDate = _dateTime.Now;

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
                    Step = request.ApprovalSteps
                        .Where(s => s.Status == (int)QCS.Domain.Enum.RequestStatus.Approved)
                        .OrderBy(s => s.Sequence)
                        .Select(s => new StepDto
                        {
                            StepName = s.StepName,
                            Approver = s.ApproverName ?? s.ApproverNId ?? "Unknown",
                            ApprovalDate = s.ActionDate ?? fallbackApprovalDate
                        }).ToList()
                },
                DrawSetting = new DrawSettingDto { Color = "#000000", FontSize = 8 }
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

    }
}