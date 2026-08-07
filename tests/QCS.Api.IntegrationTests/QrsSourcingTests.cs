using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QCS.API.Controllers;
using QCS.API.Integration;

namespace QCS.Api.IntegrationTests
{
    public class QrsSourcingTests
    {
        [Fact]
        public async Task SearchAsync_ForwardsSearchAndCapsPaging()
        {
            var handler = new RecordingHandler(HttpStatusCode.OK, "{}");
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://qrs.invalid/")
            };
            var options = new StaticOptionsMonitor<QrsIntegrationOptions>(new QrsIntegrationOptions
            {
                BaseUrl = "https://qrs.invalid/",
                ApiKey = "test-api-key"
            });
            var client = new QrsSourcingClient(httpClient, options);

            using var response = await client.SearchAsync(" bolt & nut ", 0, 500, CancellationToken.None);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            handler.Request.ShouldNotBeNull();
            handler.Request.RequestUri.ShouldNotBeNull();
            handler.Request.RequestUri.PathAndQuery.ShouldBe(
                "/api/Integration/SourcingRequests?search=bolt%20%26%20nut&page=1&pageSize=100");
            handler.Request.Headers.GetValues("X-Api-Key").Single().ShouldBe("test-api-key");
        }

        [Fact]
        public async Task Search_PassesPagingToClient()
        {
            var client = new RecordingQrsSourcingClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[]}")
            });
            var controller = new QrsSourcingController(
                client,
                NullLogger<QrsSourcingController>.Instance);

            var result = await controller.Search("needle", 3, 75, CancellationToken.None);

            result.ShouldBeOfType<ContentResult>();
            client.Search.ShouldBe(("needle", 3, 75));
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, 502)]
        [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
        [InlineData(HttpStatusCode.GatewayTimeout, 503)]
        public async Task Search_IsolatesUpstreamFailure(HttpStatusCode upstreamStatus, int expectedStatus)
        {
            var client = new RecordingQrsSourcingClient(new HttpResponseMessage(upstreamStatus));
            var controller = new QrsSourcingController(
                client,
                NullLogger<QrsSourcingController>.Instance);

            var result = await controller.Search(null, 1, 10, CancellationToken.None);

            var problem = result.ShouldBeOfType<ObjectResult>();
            problem.StatusCode.ShouldBe(expectedStatus);
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _content;

            public RecordingHandler(HttpStatusCode statusCode, string content)
            {
                _statusCode = statusCode;
                _content = content;
            }

            public HttpRequestMessage? Request { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Request = request;
                return Task.FromResult(new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_content)
                });
            }
        }

        private sealed class RecordingQrsSourcingClient : IQrsSourcingClient
        {
            private readonly HttpResponseMessage _response;

            public RecordingQrsSourcingClient(HttpResponseMessage response)
            {
                _response = response;
            }

            public (string? Search, int Page, int PageSize)? Search { get; private set; }

            public Task<HttpResponseMessage> SearchAsync(
                string? search,
                int page,
                int pageSize,
                CancellationToken cancellationToken)
            {
                Search = (search, page, pageSize);
                return Task.FromResult(_response);
            }

            public Task<HttpResponseMessage> GetByCodeAsync(
                string code,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }

        private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
        {
            public StaticOptionsMonitor(T value)
            {
                CurrentValue = value;
            }

            public T CurrentValue { get; }

            public T Get(string? name) => CurrentValue;

            public IDisposable? OnChange(Action<T, string?> listener) => null;
        }
    }
}