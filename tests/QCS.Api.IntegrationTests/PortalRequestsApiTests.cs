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
                var r1 = new Request
                {
                    Id = 1,
                    Code = "QC-20260804-001",
                    Title = "User1 Draft Widget",
                    VendorCode = "V001",
                    VendorName = "Acme Corp",
                    RequestDate = new DateTime(2026, 8, 1, 10, 0, 0),
                    Status = (int)RequestStatus.Draft,
                    CurrentStepSequence = 1,
                    CreatedBy = "USER01",
                    IsActive = true,
                    Remark = "Need urgently"
                };
                r1.ApprovalSteps.Add(new ApprovalStep { Id = 10, Sequence = 1, StepName = "Submitter", Status = (int)LegacyApprovalStepStatus.Draft, ApproverNId = null, ApproverName = null });
                r1.Quotations.Add(new Quotation { Id = 101, FileName = "widget_quote.pdf", FilePath = "/files/101.pdf", DocumentTypeId = 10, ContentType = "application/pdf" });
                db.Requests.Add(r1);

                var r2 = new Request
                {
                    Id = 2,
                    Code = "QC-20260804-002",
                    Title = "User1 Approved Gadget",
                    VendorCode = "V002",
                    VendorName = "Beta Industries",
                    RequestDate = new DateTime(2026, 8, 2, 10, 0, 0),
                    Status = (int)RequestStatus.Completed,
                    CurrentStepSequence = 99,
                    CreatedBy = "USER01",
                    IsActive = true,
                    Remark = "Completed order"
                };
                r2.ApprovalSteps.Add(new ApprovalStep { Id = 20, Sequence = 1, StepName = "Submitter", Status = (int)LegacyApprovalStepStatus.Approved, ApproverNId = "USER01", ApproverName = "User One", ActionDate = new DateTime(2026, 8, 2, 10, 5, 0) });
                r2.ApprovalSteps.Add(new ApprovalStep { Id = 21, Sequence = 2, StepName = "Manager Approval", Status = (int)LegacyApprovalStepStatus.Approved, ApproverNId = "MGR01", ApproverName = "Manager One", ActionDate = new DateTime(2026, 8, 2, 11, 0, 0), Comment = "LGTM" });
                r2.Quotations.Add(new Quotation { Id = 102, FileName = "gadget_spec.pdf", FilePath = "/files/102.pdf", DocumentTypeId = 30, ContentType = "application/pdf" });
                db.Requests.Add(r2);

                db.Requests.Add(new Request
                {
                    Id = 3,
                    Code = "QC-20260804-003",
                    Title = "User1 Rejected Tool",
                    VendorCode = "V001",
                    VendorName = "Acme Corp",
                    RequestDate = new DateTime(2026, 8, 3, 10, 0, 0),
                    Status = (int)RequestStatus.Rejected,
                    CurrentStepSequence = -1,
                    CreatedBy = "USER01",
                    IsActive = false,
                    Remark = "Out of budget"
                });

                // User 2 requests
                var r4 = new Request
                {
                    Id = 4,
                    Code = "QC-20260804-004",
                    Title = "User2 Pending Paper",
                    VendorCode = "V003",
                    VendorName = "Gamma Logistics",
                    RequestDate = new DateTime(2026, 8, 4, 10, 0, 0),
                    Status = (int)RequestStatus.InProcess,
                    CurrentStepSequence = 2,
                    CreatedBy = "USER02",
                    IsActive = true,
                    Remark = "Office supplies"
                };
                r4.ApprovalSteps.Add(new ApprovalStep { Id = 40, Sequence = 1, StepName = "Submitter", Status = (int)LegacyApprovalStepStatus.Approved, ApproverNId = "USER02", ApproverName = "User Two", ActionDate = new DateTime(2026, 8, 4, 10, 1, 0) });
                r4.ApprovalSteps.Add(new ApprovalStep { Id = 41, Sequence = 2, StepName = "Manager Approval", Status = (int)LegacyApprovalStepStatus.Pending, ApproverNId = "USER01", ApproverName = "User One" });
                db.Requests.Add(r4);

                db.Requests.Add(new Request
                {
                    Id = 5,
                    Code = "QC-20260804-005",
                    Title = "User2 Approved Printer",
                    VendorCode = "V002",
                    VendorName = "Beta Industries",
                    RequestDate = new DateTime(2026, 8, 4, 11, 0, 0),
                    Status = (int)RequestStatus.Completed,
                    CurrentStepSequence = 99,
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

        [Fact]
        public async Task GetById_ValidId_ReturnsDetailWithWorkflowStepsPermissionsDocumentsAndHistories()
        {
            var client = CreateAuthenticatedClient("USER01");
            var result = await client.GetFromJsonAsync<PortalRequestDetailDto>("/api/Portal/Requests/1");

            result.ShouldNotBeNull();
            result.Id.ShouldBe(1);
            result.Code.ShouldBe("QC-20260804-001");
            result.Title.ShouldBe("User1 Draft Widget");
            result.Status.ShouldBe((int)RequestStatus.Draft);
            result.StatusName.ShouldBe("Draft");
            result.RequesterNId.ShouldBe("USER01");
            result.Permissions.CanEdit.ShouldBeTrue();
            result.Permissions.CanDelete.ShouldBeTrue();
            result.Documents.Count.ShouldBe(1);
            result.Documents[0].Id.ShouldBe(101);
            result.Documents[0].ViewUrl.ShouldBe("/api/Request/ViewFile/101");
        }

        [Fact]
        public async Task GetById_InvalidId_Returns400ProblemDetails()
        {
            var client = CreateAuthenticatedClient("USER01");
            var response = await client.GetAsync("/api/Portal/Requests/0");
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task GetById_MissingRow_Returns404ProblemDetails()
        {
            var client = CreateAuthenticatedClient("USER01");
            var response = await client.GetAsync("/api/Portal/Requests/999999");
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task GetById_UnauthorizedUser_Returns403ProblemDetails()
        {
            // USER03 is not creator, not assigned to r1 (Draft by USER01), and r1 is not Approved.
            var client = CreateAuthenticatedClient("USER03");
            var response = await client.GetAsync("/api/Portal/Requests/1");
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task GetByCode_ValidCode_ReturnsDetailSameAsId()
        {
            var client = CreateAuthenticatedClient("USER01");
            var result = await client.GetFromJsonAsync<PortalRequestDetailDto>("/api/Portal/Requests/by-code/QC-20260804-001");

            result.ShouldNotBeNull();
            result.Id.ShouldBe(1);
            result.Code.ShouldBe("QC-20260804-001");
        }

        [Fact]
        public async Task GetByCode_MissingCode_Returns404ProblemDetails()
        {
            var client = CreateAuthenticatedClient("USER01");
            var response = await client.GetAsync("/api/Portal/Requests/by-code/NONEXISTENT");
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        [Fact]
        public async Task GetById_ApprovedStatus_IncludesFinalPdfInDocuments()
        {
            var client = CreateAuthenticatedClient("USER01");
            var result = await client.GetFromJsonAsync<PortalRequestDetailDto>("/api/Portal/Requests/2");

            result.ShouldNotBeNull();
            result.Id.ShouldBe(2);
            result.StatusName.ShouldBe("Completed");
            result.Documents.ShouldContain(d => d.DocumentTypeName == "FinalPdf" && d.ViewUrl == "/api/Quotation/ViewFile/2");
            result.Histories.Count.ShouldBeGreaterThan(0);
        }
    }
}
