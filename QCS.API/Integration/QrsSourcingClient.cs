using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace QCS.API.Integration
{
    public interface IQrsSourcingClient
    {
        Task<HttpResponseMessage> SearchAsync(string? search, CancellationToken cancellationToken);
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

        public Task<HttpResponseMessage> SearchAsync(string? search, CancellationToken cancellationToken)
        {
            var query = string.IsNullOrWhiteSpace(search)
                ? "?page=1&pageSize=50"
                : $"?search={Uri.EscapeDataString(search)}&page=1&pageSize=50";

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