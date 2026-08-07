using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QCS.Application.Abstractions;

namespace QCS.Infrastructure.Integration
{
    public sealed class QrsSourcingService : IQrsSourcingService
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsMonitor<QrsIntegrationOptions> _options;
        private readonly ILogger<QrsSourcingService> _logger;

        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public QrsSourcingService(
            HttpClient httpClient,
            IOptionsMonitor<QrsIntegrationOptions> options,
            ILogger<QrsSourcingService> logger)
        {
            _httpClient = httpClient;
            _options = options;
            _logger = logger;
        }

        public async Task<QrsSourcingPagedResultDto> GetRequestsAsync(
            string? search,
            int page,
            int pageSize,
            string? intent,
            CancellationToken cancellationToken = default)
        {
            var validPage = Math.Max(1, page);
            var validPageSize = Math.Clamp(pageSize <= 0 ? 10 : pageSize, 1, 100);

            var queryParams = new List<string>
            {
                $"page={validPage}",
                $"pageSize={validPageSize}"
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                queryParams.Add($"search={Uri.EscapeDataString(search.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(intent))
            {
                queryParams.Add($"intent={Uri.EscapeDataString(intent.Trim())}");
            }

            var path = $"api/Integration/SourcingRequests?{string.Join("&", queryParams)}";
            using var response = await SendAsync(path, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("QRS sourcing search returned upstream status code {StatusCode}.", (int)response.StatusCode);
                throw new QrsSourcingException($"QRS sourcing search failed with status {(int)response.StatusCode}", (int)response.StatusCode);
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var result = await JsonSerializer.DeserializeAsync<QrsSourcingPagedResultDto>(stream, JsonSerializerOptions, cancellationToken);
                var pageResult = result ?? new QrsSourcingPagedResultDto();
                foreach (var item in pageResult.Items)
                {
                    ValidateContract(item.Code, item.RequestType, item.Intent);
                }

                if (!string.IsNullOrWhiteSpace(intent)
                    && Enum.TryParse<QrsRequestIntent>(intent.Trim(), ignoreCase: true, out var requestedIntent)
                    && pageResult.Items.Any(item => item.Intent != (int)requestedIntent))
                {
                    throw new QrsSourcingException(
                        "QRS sourcing response did not match the requested intent.",
                        isContractViolation: true);
                }

                return pageResult;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                _logger.LogWarning(ex, "Failed to deserialize QRS sourcing search response.");
                throw new QrsSourcingException(
                    "Failed to parse QRS sourcing response.",
                    innerException: ex,
                    isContractViolation: true);
            }
            catch (Exception ex) when (ex is not QrsSourcingException)
            {
                _logger.LogWarning(ex, "Failed to read QRS sourcing search response.");
                throw new QrsSourcingException("Failed to read QRS sourcing response.", innerException: ex);
            }
        }

        public async Task<QrsSourcingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var path = $"api/Integration/SourcingRequests/{Uri.EscapeDataString(code.Trim())}";
            using var response = await SendAsync(path, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("QRS sourcing get-by-code returned upstream status code {StatusCode}.", (int)response.StatusCode);
                throw new QrsSourcingException($"QRS sourcing get-by-code failed with status {(int)response.StatusCode}", (int)response.StatusCode);
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var detail = await JsonSerializer.DeserializeAsync<QrsSourcingDetailDto>(stream, JsonSerializerOptions, cancellationToken);
                if (detail != null)
                {
                    ValidateContract(detail.Code, detail.RequestType, detail.Intent);
                }

                return detail;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                _logger.LogWarning(ex, "Failed to deserialize QRS sourcing detail response.");
                throw new QrsSourcingException(
                    "Failed to parse QRS sourcing detail response.",
                    innerException: ex,
                    isContractViolation: true);
            }
            catch (Exception ex) when (ex is not QrsSourcingException)
            {
                _logger.LogWarning(ex, "Failed to read QRS sourcing detail response.");
                throw new QrsSourcingException("Failed to read QRS sourcing detail response.", innerException: ex);
            }
        }

        private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken cancellationToken)
        {
            var options = _options.CurrentValue;
            if (string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.ApiKey))
            {
                throw new QrsSourcingException("QRS integration is not configured.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Add("X-Api-Key", options.ApiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is not QrsSourcingException)
            {
                _logger.LogWarning(ex, "Network or configuration error communicating with QRS service.");
                throw new QrsSourcingException("Network failure communicating with QRS service.", statusCode: (int)HttpStatusCode.ServiceUnavailable, innerException: ex);
            }
        }

        private static void ValidateContract(string code, int requestType, int intent)
        {
            if (!Enum.IsDefined(typeof(QrsRequestType), requestType))
            {
                throw new QrsSourcingException(
                    $"QRS request '{code}' has unrecognized request type '{requestType}'.",
                    isContractViolation: true);
            }

            if (!Enum.IsDefined(typeof(QrsRequestIntent), intent))
            {
                throw new QrsSourcingException(
                    $"QRS request '{code}' has unrecognized intent '{intent}'.",
                    isContractViolation: true);
            }
        }
    }
}
