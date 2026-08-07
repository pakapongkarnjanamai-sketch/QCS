using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QCS.API.Controllers;
using QCS.Application.Abstractions;
using QCS.Infrastructure.Integration;
using Shouldly;
using Xunit;

namespace QCS.Api.IntegrationTests
{
    public class QrsSourcingTests
    {
        [Fact]
        public async Task GetRequestsAsync_ForwardsSearchIntentAndCapsPaging()
        {
            var jsonResponse = """
            {
                "items": [
                    {
                        "code": "QRS-001",
                        "title": "Bolt & Nut",
                        "requestType": 0,
                        "requestTypeName": "Goods",
                        "requesterNId": "EMP001",
                        "requesterName": "John",
                        "currency": "THB",
                        "estimatedTotal": 1000,
                        "isUrgent": false,
                        "itemCount": 1,
                        "attachmentCount": 1,
                        "intent": 1,
                        "intentName": "Renewal"
                    }
                ],
                "pageNumber": 1,
                "pageSize": 100,
                "totalPages": 1,
                "totalCount": 1,
                "hasPreviousPage": false,
                "hasNextPage": false
            }
            """;

            var handler = new RecordingHandler(HttpStatusCode.OK, jsonResponse);
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://qrs.invalid/")
            };
            var options = new StaticOptionsMonitor<QrsIntegrationOptions>(new QrsIntegrationOptions
            {
                BaseUrl = "https://qrs.invalid/",
                ApiKey = "test-api-key"
            });
            var service = new QrsSourcingService(httpClient, options, NullLogger<QrsSourcingService>.Instance);

            var result = await service.GetRequestsAsync(" bolt & nut ", 0, 500, "Renewal", CancellationToken.None);

            result.ShouldNotBeNull();
            result.Items.Count.ShouldBe(1);
            result.Items[0].Code.ShouldBe("QRS-001");
            result.Items[0].IntentName.ShouldBe("Renewal");

            handler.Request.ShouldNotBeNull();
            handler.Request.RequestUri.ShouldNotBeNull();
            handler.Request.RequestUri.PathAndQuery.ShouldBe(
                "/api/Integration/SourcingRequests?page=1&pageSize=100&search=bolt%20%26%20nut&intent=Renewal");
            handler.Request.Headers.GetValues("X-Api-Key").Single().ShouldBe("test-api-key");
        }

        [Fact]
        public async Task Search_PassesPagingAndIntentToService()
        {
            var service = new RecordingQrsSourcingService(new QrsSourcingPagedResultDto());
            var controller = new QrsSourcingController(
                service,
                NullLogger<QrsSourcingController>.Instance);

            var result = await controller.Search("needle", 3, 75, "New", CancellationToken.None);

            var okResult = result.ShouldBeOfType<OkObjectResult>();
            okResult.Value.ShouldBeOfType<QrsSourcingPagedResultDto>();
            service.SearchCall.ShouldBe(("needle", 3, 75, "New"));
        }

        [Theory]
        [InlineData(99, 0)]
        [InlineData(0, 99)]
        public async Task GetRequestsAsync_UnknownContractEnum_IsRejected(int requestType, int intent)
        {
            var jsonResponse = $$"""
            {
                "items": [
                    {
                        "code": "QRS-UNKNOWN",
                        "title": "Unknown contract",
                        "requestType": {{requestType}},
                        "requestTypeName": "Unknown",
                        "requesterNId": "EMP001",
                        "requesterName": "John",
                        "currency": "THB",
                        "estimatedTotal": 1000,
                        "isUrgent": false,
                        "itemCount": 1,
                        "attachmentCount": 1,
                        "intent": {{intent}},
                        "intentName": "Unknown"
                    }
                ],
                "pageNumber": 1,
                "pageSize": 10,
                "totalPages": 1,
                "totalCount": 1,
                "hasPreviousPage": false,
                "hasNextPage": false
            }
            """;
            var service = CreateService(new RecordingHandler(HttpStatusCode.OK, jsonResponse));

            await Should.ThrowAsync<QrsSourcingException>(() =>
                service.GetRequestsAsync(null, 1, 10, null, CancellationToken.None));
        }

        [Fact]
        public async Task GetRequestsAsync_ResponseIntentDoesNotMatchFilter_IsRejected()
        {
            var jsonResponse = """
            {
                "items": [
                    {
                        "code": "QRS-NEW-ONLY",
                        "title": "New request",
                        "requestType": 0,
                        "requestTypeName": "Goods",
                        "requesterNId": "EMP001",
                        "requesterName": "John",
                        "currency": "THB",
                        "estimatedTotal": 1000,
                        "isUrgent": false,
                        "itemCount": 1,
                        "attachmentCount": 1,
                        "intent": 0,
                        "intentName": "New"
                    }
                ],
                "pageNumber": 1,
                "pageSize": 10,
                "totalPages": 1,
                "totalCount": 1,
                "hasPreviousPage": false,
                "hasNextPage": false
            }
            """;
            var service = CreateService(new RecordingHandler(HttpStatusCode.OK, jsonResponse));

            await Should.ThrowAsync<QrsSourcingException>(() =>
                service.GetRequestsAsync(null, 1, 10, "Renewal", CancellationToken.None));
        }

        [Fact]
        public async Task GetRequestsAsync_CallerCancellation_IsPreserved()
        {
            var service = CreateService(new CancelingHandler());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Should.ThrowAsync<OperationCanceledException>(() =>
                service.GetRequestsAsync(null, 1, 10, null, cancellation.Token));
        }

        [Theory]
        [InlineData(StatusCodes.Status500InternalServerError, StatusCodes.Status502BadGateway)]
        [InlineData(StatusCodes.Status400BadRequest, StatusCodes.Status502BadGateway)]
        [InlineData(StatusCodes.Status503ServiceUnavailable, StatusCodes.Status503ServiceUnavailable)]
        [InlineData(StatusCodes.Status504GatewayTimeout, StatusCodes.Status503ServiceUnavailable)]
        [InlineData(null, StatusCodes.Status503ServiceUnavailable)]
        public async Task Search_IsolatesUpstreamFailure(int? upstreamStatus, int expectedStatus)
        {
            var service = new FailingQrsSourcingService(new QrsSourcingException("Upstream sensitive error details", upstreamStatus));
            var controller = new QrsSourcingController(
                service,
                NullLogger<QrsSourcingController>.Instance);

            var result = await controller.Search(null, 1, 10, null, CancellationToken.None);

            var problem = result.ShouldBeOfType<ObjectResult>();
            problem.StatusCode.ShouldBe(expectedStatus);
            var problemDetails = problem.Value.ShouldBeOfType<ProblemDetails>();
            problemDetails.Title.ShouldBe("QRS sourcing lookup is unavailable.");
            problemDetails.Detail.ShouldBeNull();
        }

        [Fact]
        public async Task Search_InvalidUpstreamContract_Returns502WithoutDetail()
        {
            var service = new FailingQrsSourcingService(new QrsSourcingException(
                "Sensitive invalid payload",
                isContractViolation: true));
            var controller = new QrsSourcingController(
                service,
                NullLogger<QrsSourcingController>.Instance);

            var result = await controller.Search(null, 1, 10, null, CancellationToken.None);

            var problem = result.ShouldBeOfType<ObjectResult>();
            problem.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
            var details = problem.Value.ShouldBeOfType<ProblemDetails>();
            details.Title.ShouldBe("QRS sourcing lookup is unavailable.");
            details.Detail.ShouldBeNull();
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

        private sealed class CancelingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
                Task.FromCanceled<HttpResponseMessage>(cancellationToken);
        }

        private static QrsSourcingService CreateService(HttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://qrs.invalid/")
            };
            var options = new StaticOptionsMonitor<QrsIntegrationOptions>(new QrsIntegrationOptions
            {
                BaseUrl = "https://qrs.invalid/",
                ApiKey = "test-api-key"
            });
            return new QrsSourcingService(httpClient, options, NullLogger<QrsSourcingService>.Instance);
        }

        private sealed class RecordingQrsSourcingService : IQrsSourcingService
        {
            private readonly QrsSourcingPagedResultDto _result;

            public RecordingQrsSourcingService(QrsSourcingPagedResultDto result)
            {
                _result = result;
            }

            public (string? Search, int Page, int PageSize, string? Intent)? SearchCall { get; private set; }

            public Task<QrsSourcingPagedResultDto> GetRequestsAsync(
                string? search,
                int page,
                int pageSize,
                string? intent,
                CancellationToken cancellationToken = default)
            {
                SearchCall = (search, page, pageSize, intent);
                return Task.FromResult(_result);
            }

            public Task<QrsSourcingDetailDto?> GetByCodeAsync(
                string code,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }

        private sealed class FailingQrsSourcingService : IQrsSourcingService
        {
            private readonly Exception _exception;

            public FailingQrsSourcingService(Exception exception)
            {
                _exception = exception;
            }

            public Task<QrsSourcingPagedResultDto> GetRequestsAsync(
                string? search,
                int page,
                int pageSize,
                string? intent,
                CancellationToken cancellationToken = default)
            {
                throw _exception;
            }

            public Task<QrsSourcingDetailDto?> GetByCodeAsync(
                string code,
                CancellationToken cancellationToken = default) =>
                throw _exception;
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