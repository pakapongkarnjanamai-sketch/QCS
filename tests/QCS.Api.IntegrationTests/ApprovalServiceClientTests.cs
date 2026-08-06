using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QCS.Application.Abstractions;
using QCS.Infrastructure.Approval;
using Shouldly;
using Xunit;

namespace QCS.Api.IntegrationTests
{
    public sealed class ApprovalServiceClientTests
    {
        [Fact]
        public void ApiStartup_WithoutForwardedUserSecret_FailsFast()
        {
            using var factory = new MissingApprovalSecretFactory();

            var exception = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

            exception.Failures.ShouldContain(failure =>
                failure.Contains("ForwardedUserSecret is required", StringComparison.Ordinal));
        }

        [Fact]
        public void ApprovalRequestFactory_UsesConfiguredDeepLinkAndStableConditionalKeys()
        {
            var options = CreateOptions();
            var factory = new ApprovalRequestFactory(
                new StaticOptionsMonitor<ApprovalServiceOptions>(options));

            var request = factory.Build(new ApprovalRequestContext(
                "Test request",
                "QC-001",
                42,
                "GPD",
                "V001",
                new DateTime(2026, 8, 1),
                new DateTime(2026, 8, 31),
                3));

            request.SourceUrl.ShouldBe("https://approval.invalid/QCS/User/requests/42");
            request.ConditionalData.Keys.ShouldBe(
                new[] { "vendorCode", "validFrom", "validUntil", "attachmentCount", "sourceSystem" },
                ignoreOrder: true);
            request.ConditionalData["sourceSystem"].ShouldBe("QCS");
        }

        [Fact]
        public async Task GetDocument_SendsForwardedIdentityHeaders()
        {
            var handler = new RecordingHandler(_ => JsonResponse(new
            {
                data = new { id = Guid.NewGuid(), status = "Draft" },
                success = true,
                message = (string?)null
            }));
            var client = CreateClient(handler);

            await client.GetDocumentAsync(Guid.NewGuid(), "USER01");

            var request = handler.Requests.ShouldHaveSingleItem();
            request.Headers["X-Gpcs-Authenticated-User"].ShouldBe("USER01");
            request.Headers["X-Gpcs-Authentication-Type"].ShouldBe("QCS");
            request.Headers["X-Gpcs-Auth-Secret"].ShouldBe("test-forwarded-secret");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("InProgress")]
        [InlineData("FutureStatus")]
        public async Task GetDocument_WithMissingOrUnknownStatus_Throws(string? status)
        {
            var handler = new RecordingHandler(_ => JsonResponse(new
            {
                data = new { id = Guid.NewGuid(), status },
                success = true,
                message = (string?)null
            }));
            var client = CreateClient(handler);

            await Should.ThrowAsync<InvalidOperationException>(
                () => client.GetDocumentAsync(Guid.NewGuid(), "USER01"));
        }

        [Fact]
        public async Task GetDocument_WhenEnvelopeReportsFailure_Throws()
        {
            var handler = new RecordingHandler(_ => JsonResponse(new
            {
                data = (object?)null,
                success = false,
                message = "Workflow resolution failed"
            }));
            var client = CreateClient(handler);

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => client.GetDocumentAsync(Guid.NewGuid(), "USER01"));
            exception.Message.ShouldContain("reported failure");
        }

        [Fact]
        public async Task GetDocument_WhenHttpFails_DoesNotExposeForwardedSecret()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("upstream failed")
            });
            var client = CreateClient(handler);

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => client.GetDocumentAsync(Guid.NewGuid(), "USER01"));
            exception.Message.ShouldNotContain("test-forwarded-secret");
        }

        [Fact]
        public async Task ListPendingDocumentIds_ReadsEveryPage()
        {
            var ids = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray();
            var handler = new RecordingHandler(request =>
            {
                var isFirstPage = request.RequestUri!.Query.Contains("page=1", StringComparison.Ordinal);
                var pageIds = isFirstPage ? ids.Take(200) : ids.Skip(200);
                return JsonResponse(new
                {
                    data = new
                    {
                        items = pageIds.Select(id => new { id }),
                        totalCount = ids.Length
                    },
                    success = true,
                    message = (string?)null
                });
            });
            var client = CreateClient(handler);

            var result = await client.ListPendingDocumentIdsAsync("USER01");

            result.ShouldBe(ids);
            handler.Requests.Count.ShouldBe(2);
            handler.Requests[0].Uri.Query.ShouldContain("page=1");
            handler.Requests[1].Uri.Query.ShouldContain("page=2");
        }

        private static ApprovalServiceClient CreateClient(RecordingHandler handler)
        {
            var documentClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://approval.invalid/Document/")
            };
            var workflowClient = new HttpClient(new RecordingHandler(_ => throw new InvalidOperationException("Unexpected workflow call")))
            {
                BaseAddress = new Uri("https://approval.invalid/Workflow/")
            };
            var options = CreateOptions();

            return new ApprovalServiceClient(
                documentClient,
                new StubHttpClientFactory(workflowClient),
                new StaticOptionsMonitor<ApprovalServiceOptions>(options),
                NullLogger<ApprovalServiceClient>.Instance);
        }

            private static ApprovalServiceOptions CreateOptions() => new()
            {
                DocumentBaseUrl = "https://approval.invalid/Document",
                WorkflowBaseUrl = "https://approval.invalid/Workflow",
                SourceSystem = "QCS",
                DocumentTypeCode = "QC",
                DocumentTypeName = "Quotation Comparison",
                RequestUrlTemplate = "https://approval.invalid/QCS/User/requests/{id}",
                ForwardedUserSecret = "test-forwarded-secret"
            };

        private static HttpResponseMessage JsonResponse(object body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json")
            };

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

            public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                _respond = respond;
            }

            public List<RecordedRequest> Requests { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Requests.Add(new RecordedRequest(
                    request.RequestUri!,
                    request.Headers.ToDictionary(
                        header => header.Key,
                        header => string.Join(",", header.Value),
                        StringComparer.OrdinalIgnoreCase)));
                return Task.FromResult(_respond(request));
            }
        }

        private sealed record RecordedRequest(Uri Uri, IReadOnlyDictionary<string, string> Headers);

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;

            public StubHttpClientFactory(HttpClient client)
            {
                _client = client;
            }

            public HttpClient CreateClient(string name) => _client;
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

        private sealed class MissingApprovalSecretFactory : QcsApiFactory
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ExternalServices:Approval:ForwardedUserSecret"] = string.Empty
                    }));
            }
        }
    }
}