using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QCS.API.Controllers;
using QCS.Domain.DTOs;

namespace QCS.Api.IntegrationTests
{
    public sealed class VendorControllerTests
    {
        [Fact]
        public async Task GetActiveVendorLookup_NormalizesCodeAndNameFromVerifiedUpstreamContract()
        {
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [
                      { "Id": 1, "Name": " CEBU IWAKAMI CORPORATION ", "Code": "10001665" },
                      { "id": 2, "name": "SIAM CELEBRITY CO.,LTD.", "code": " 10001668 " },
                      { "Id": 3, "Name": " ", "Code": " " }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
            var controller = CreateController(handler);

            var result = await controller.GetActiveVendorLookup(CancellationToken.None);

            var ok = result.Result.ShouldBeOfType<OkObjectResult>();
            ok.Value.ShouldBeAssignableTo<IReadOnlyList<ActiveVendorLookupDto>>();
            var vendors = (IReadOnlyList<ActiveVendorLookupDto>)ok.Value!;
            vendors.Count.ShouldBe(2);
            vendors[0].Id.ShouldBe(1);
            vendors[0].Name.ShouldBe("CEBU IWAKAMI CORPORATION");
            vendors[0].Code.ShouldBe("10001665");
            vendors[1].Id.ShouldBe(2);
            vendors[1].Name.ShouldBe("SIAM CELEBRITY CO.,LTD.");
            vendors[1].Code.ShouldBe("10001668");
            handler.RequestPath.ShouldBe("/api/Vendors/LookupActive");
        }

        [Fact]
        public async Task GetActiveVendorLookup_WhenUpstreamFails_Returns502WithoutLeakingBody()
        {
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("sensitive upstream failure")
            });
            var controller = CreateController(handler);

            var result = await controller.GetActiveVendorLookup(CancellationToken.None);

            var problem = result.Result.ShouldBeOfType<ObjectResult>();
            problem.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
            var details = problem.Value.ShouldBeOfType<ProblemDetails>();
            details.Title.ShouldBe("Vendor lookup unavailable");
            details.Detail.ShouldNotBeNull();
            details.Detail.ShouldNotContain("sensitive upstream failure");
        }

        private static VendorController CreateController(HttpMessageHandler handler)
        {
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://vendor.test/api/")
            };
            return new VendorController(new StubHttpClientFactory(client), null!);
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpClient _client;

            public StubHttpClientFactory(HttpClient client)
            {
                _client = client;
            }

            public HttpClient CreateClient(string name)
            {
                name.ShouldBe("VendorApi");
                return _client;
            }
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            public string? RequestPath { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestPath = request.RequestUri?.AbsolutePath;
                return Task.FromResult(_responseFactory(request));
            }
        }
    }
}