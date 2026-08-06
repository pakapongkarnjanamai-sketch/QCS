using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QCS.Application.Abstractions;
using QCS.Infrastructure.Approval;

namespace QCS.Api.IntegrationTests
{
    public sealed class ApprovalWorkflowTransportTests
    {
        [Fact]
        public async Task PreviewRoute_UsesWorkflowOpenApiNamesAndAcceptsNumericVersion()
        {
            var handler = new CaptureHandler();
            var workflowClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://workflow.test/")
            };
            var options = new ApprovalServiceOptions
            {
                DocumentTypeCode = "QC",
                DocumentTypeName = "Quotation Comparison",
                SourceSystem = "QCS",
                ForwardedUserSecret = "test-secret"
            };
            var client = new ApprovalServiceClient(
                new HttpClient { BaseAddress = new Uri("https://document.test/") },
                new StubHttpClientFactory(workflowClient),
                new StubOptionsMonitor<ApprovalServiceOptions>(options),
                NullLogger<ApprovalServiceClient>.Instance);
            var request = new ApprovalDocumentRequest(
                "Preview",
                "PREVIEW",
                "https://qcs.test/preview",
                false,
                "ORG1",
                new[] { "ORG1" },
                new Dictionary<string, string?>
                {
                    ["vendorCode"] = "V001",
                    ["attachmentCount"] = "3"
                });

            var result = await client.PreviewRouteAsync(request, "USER01");

            result.WorkflowVersion.ShouldBe("2");
            handler.Payload.ShouldNotBeNull();
            using var payload = JsonDocument.Parse(handler.Payload);
            var root = payload.RootElement;
            root.GetProperty("documentType").GetString().ShouldBe("QC");
            root.GetProperty("requesterUsername").GetString().ShouldBe("USER01");
            root.GetProperty("conditionalData").GetProperty("attachmentCount").GetString().ShouldBe("3");
            root.TryGetProperty("documentTypeCode", out _).ShouldBeFalse();
            root.TryGetProperty("requesterNId", out _).ShouldBeFalse();
        }

        private sealed class CaptureHandler : HttpMessageHandler
        {
            public string? Payload { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                request.RequestUri!.ToString().ShouldEndWith("api/workflows/resolve-preview");
                Payload = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "data": {
                            "name": "QCS Workflow",
                            "version": 2,
                            "steps": [
                              {
                                "sequence": 1,
                                "stepName": "Requester",
                                "isFinalStep": false,
                                "assignees": []
                              }
                            ]
                          },
                          "success": true,
                          "message": null
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _workflowClient;

            public StubHttpClientFactory(HttpClient workflowClient)
            {
                _workflowClient = workflowClient;
            }

            public HttpClient CreateClient(string name)
            {
                name.ShouldBe("ApprovalWorkflow");
                return _workflowClient;
            }
        }

        private sealed class StubOptionsMonitor<T> : IOptionsMonitor<T>
        {
            public StubOptionsMonitor(T value)
            {
                CurrentValue = value;
            }

            public T CurrentValue { get; }

            public T Get(string? name) => CurrentValue;

            public IDisposable? OnChange(Action<T, string?> listener) => null;
        }
    }
}