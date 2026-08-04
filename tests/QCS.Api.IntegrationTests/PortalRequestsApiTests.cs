using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using QCS.Domain.DTOs.Portal;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using Xunit;
using Shouldly;

namespace QCS.Api.IntegrationTests
{
    public class PortalRequestsApiTests : IClassFixture<QcsApiFactory>
    {
        private readonly QcsApiFactory _factory;

        public PortalRequestsApiTests(QcsApiFactory factory)
        {
            _factory = factory;
            SeedDefaultData();
        }

        private void SeedDefaultData()
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any()) return;

                // User 1 requests
                db.Requests.Add(new Request
                {
                    Id = 1,
                    Code = "QC-20260804-001",
                    Title = "User1 Draft Widget",
                    VendorCode = "V001",
                    VendorName = "Acme Corp",
                    RequestDate = new DateTime(2026, 8, 1, 10, 0, 0),
                    Status = (int)RequestStatus.Draft,
                    CurrentStepId = 1,
                    CreatedBy = "USER01",
                    IsActive = true,
                    Remark = "Need urgently"
                });

                db.Requests.Add(new Request
                {
                    Id = 2,
                    Code = "QC-20260804-002",
                    Title = "User1 Approved Gadget",
                    VendorCode = "V002",
                    VendorName = "Beta Industries",
                    RequestDate = new DateTime(2026, 8, 2, 10, 0, 0),
                    Status = (int)RequestStatus.Approved,
                    CurrentStepId = 99,
                    CreatedBy = "USER01",
                    IsActive = true,
                    Remark = "Completed order"
                });

                db.Requests.Add(new Request
                {
                    Id = 3,
                    Code = "QC-20260804-003",
                    Title = "User1 Rejected Tool",
                    VendorCode = "V001",
                    VendorName = "Acme Corp",
                    RequestDate = new DateTime(2026, 8, 3, 10, 0, 0),
                    Status = (int)RequestStatus.Rejected,
                    CurrentStepId = -1,
                    CreatedBy = "USER01",
                    IsActive = false,
                    Remark = "Out of budget"
                });

                // User 2 requests
                db.Requests.Add(new Request
                {
                    Id = 4,
                    Code = "QC-20260804-004",
                    Title = "User2 Pending Paper",
                    VendorCode = "V003",
                    VendorName = "Gamma Logistics",
                    RequestDate = new DateTime(2026, 8, 4, 10, 0, 0),
                    Status = (int)RequestStatus.Pending,
                    CurrentStepId = 2,
                    CreatedBy = "USER02",
                    IsActive = true,
                    Remark = "Office supplies"
                });

                db.Requests.Add(new Request
                {
                    Id = 5,
                    Code = "QC-20260804-005",
                    Title = "User2 Approved Printer",
                    VendorCode = "V002",
                    VendorName = "Beta Industries",
                    RequestDate = new DateTime(2026, 8, 4, 11, 0, 0),
                    Status = (int)RequestStatus.Approved,
                    CurrentStepId = 99,
                    CreatedBy = "USER02",
                    IsActive = true,
                    Remark = "Heavy duty printer"
                });
            });
        }

        private HttpClient CreateAuthenticatedClient(string userNid = "USER01")
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, $"NIKONOA\\{userNid}");
            return client;
        }

        [Fact]
        public async Task AnonymousCall_IsRejectedWith401()
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/api/Portal/Requests?view=MyRequests");
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task InvalidView_Returns400ProblemDetails()
        {
            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/Portal/Requests?view=UnknownView");
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task ViewSelection_MyRequests_ReturnsDraftAndPendingForCurrentUserOnly()
        {
            var client = CreateAuthenticatedClient("USER01");
            var result = await client.GetFromJsonAsync<PortalPage<PortalRequestListItemDto>>("/api/Portal/Requests?view=MyRequests");

            result.ShouldNotBeNull();
            result.TotalCount.ShouldBe(1);
            result.Items.Count.ShouldBe(1);
            result.Items[0].Code.ShouldBe("QC-20260804-001");
            result.Items[0].Title.ShouldBe("User1 Draft Widget");
        }

        [Fact]
        public async Task ViewSelection_MyApproved_ReturnsOnlyApprovedForCurrentUser()
        {
            var client = CreateAuthenticatedClient("USER01");
            var result = await client.GetFromJsonAsync<PortalPage<PortalRequestListItemDto>>("/api/Portal/Requests?view=MyApproved");

            result.ShouldNotBeNull();
            result.TotalCount.ShouldBe(1);
            result.Items[0].Code.ShouldBe("QC-20260804-002");
        }

        [Fact]
        public async Task ViewSelection_Rejected_ReturnsOnlyRejectedForCurrentUser()
        {
            var client = CreateAuthenticatedClient("USER01");
            var result = await client.GetFromJsonAsync<PortalPage<PortalRequestListItemDto>>("/api/Portal/Requests?view=Rejected");

            result.ShouldNotBeNull();
            result.TotalCount.ShouldBe(1);
            result.Items[0].Code.ShouldBe("QC-20260804-003");
        }

        [Fact]
        public async Task ViewSelection_AllApproved_ReturnsAllApprovedForAnyUser()
        {
            var client = CreateAuthenticatedClient("USER01");
            var result = await client.GetFromJsonAsync<PortalPage<PortalRequestListItemDto>>("/api/Portal/Requests?view=AllApproved");

            result.ShouldNotBeNull();
            result.TotalCount.ShouldBe(2);
        }

        [Fact]
        public async Task OwnershipIsolation_User2CannotSeeUser1MyRequests()
        {
            var client = CreateAuthenticatedClient("USER02");
            var result = await client.GetFromJsonAsync<PortalPage<PortalRequestListItemDto>>("/api/Portal/Requests?view=MyRequests");

            result.ShouldNotBeNull();
            result.TotalCount.ShouldBe(1);
            result.Items[0].Code.ShouldBe("QC-20260804-004");
        }

        [Fact]
        public async Task Search_FiltersByTitleOrVendorName()
        {
            var client = CreateAuthenticatedClient("USER01");
            var result = await client.GetFromJsonAsync<PortalPage<PortalRequestListItemDto>>("/api/Portal/Requests?view=AllApproved&search=Beta");

            result.ShouldNotBeNull();
            result.TotalCount.ShouldBe(2);
        }

        [Fact]
        public async Task Pagination_ClampsPageSizeAndCalculatesHasNextPage()
        {
            var client = CreateAuthenticatedClient("USER01");
            var result = await client.GetFromJsonAsync<PortalPage<PortalRequestListItemDto>>("/api/Portal/Requests?view=AllApproved&page=1&pageSize=1");

            result.ShouldNotBeNull();
            result.Page.ShouldBe(1);
            result.PageSize.ShouldBe(1);
            result.TotalCount.ShouldBe(2);
            result.HasNextPage.ShouldBeTrue();
            result.Items.Count.ShouldBe(1);
        }
    }
}
