using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace QCS.API.Integration
{
    public interface IQrsSourcingClient
    {
        Task<HttpResponseMessage> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
        Task<HttpResponseMessage> GetByCodeAsync(string code, CancellationToken cancellationToken);
    }

    public sealed class QrsSourcingClient : IQrsSourcingClient
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsMonitor<QrsIntegrationOptions> _options;

        public QrsSourcingClient(HttpClient httpClient, IOptionsMonitor<QrsIntegrationOptions> options)
        {
            _httpClient = httpClient;
            _options = options;
        }

        public Task<HttpResponseMessage> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
        {
            var validPage = Math.Max(1, page);
            var validPageSize = Math.Clamp(pageSize <= 0 ? 10 : pageSize, 1, 100);
            var query = string.IsNullOrWhiteSpace(search)
                ? $"?page={validPage}&pageSize={validPageSize}"
                : $"?search={Uri.EscapeDataString(search.Trim())}&page={validPage}&pageSize={validPageSize}";

            return SendAsync($"api/Integration/SourcingRequests{query}", cancellationToken);
        }

        public Task<HttpResponseMessage> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
            SendAsync($"api/Integration/SourcingRequests/{Uri.EscapeDataString(code)}", cancellationToken);

        private Task<HttpResponseMessage> SendAsync(string path, CancellationToken cancellationToken)
        {
            var options = _options.CurrentValue;
            if (string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.ApiKey))
            {
                throw new InvalidOperationException("QRS integration is not configured.");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Api-Key", options.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
    }
}