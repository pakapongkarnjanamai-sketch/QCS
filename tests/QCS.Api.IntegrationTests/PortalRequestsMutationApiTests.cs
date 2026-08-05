using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using QCS.Domain.DTOs.Portal;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using Shouldly;
using Xunit;

namespace QCS.Api.IntegrationTests
{
    public class PortalRequestsMutationApiTests : IClassFixture<QcsApiFactory>
    {
        private readonly QcsApiFactory _factory;

        public PortalRequestsMutationApiTests(QcsApiFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateAuthenticatedClient(string userNid = "USER01")
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, $"NIKONOA\\{userNid}");
            return client;
        }

        [Fact]
        public async Task CreatePortalDraft_WithMinimalData_Returns200WithIdAndCode()
        {
            var client = CreateAuthenticatedClient("USER01");
            var dto = new SavePortalRequestDto
            {
                Title = "Test Portal Draft"
            };

            var response = await client.PostAsJsonAsync("/api/Portal/Requests", dto);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<PortalSaveResultDto>();
            result.ShouldNotBeNull();
            result.Id.ShouldBeGreaterThan(0);
            result.Code.ShouldStartWith("QC-");
        }

        [Fact]
        public async Task UpdatePortalDraft_WhenOwner_UpdatesMetadata()
        {
            int requestId = 901;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(requestId) != null) return;
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = "QC-20260804-901",
                    Title = "Initial Title",
                    VendorCode = "V000",
                    VendorName = "Initial Vendor",
                    Status = (int)RequestStatus.Draft,
                    CurrentStepId = 1,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER01");
            var dto = new SavePortalRequestDto
            {
                Title = "Updated Draft Title",
                VendorCode = "V999",
                VendorName = "Test Vendor"
            };

            var response = await client.PutAsJsonAsync($"/api/Portal/Requests/{requestId}", dto);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var detailResponse = await client.GetAsync($"/api/Portal/Requests/{requestId}");
            detailResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var detail = await detailResponse.Content.ReadFromJsonAsync<PortalRequestDetailDto>();
            detail.ShouldNotBeNull();
            detail.Title.ShouldBe("Updated Draft Title");
            detail.VendorCode.ShouldBe("V999");
        }

        [Fact]
        public async Task UpdatePortalDraft_WhenNotOwner_Returns403Forbidden()
        {
            int requestId = 902;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(requestId) != null) return;
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = "QC-20260804-902",
                    Title = "User 2 Draft",
                    VendorCode = "V000",
                    VendorName = "Initial Vendor",
                    Status = (int)RequestStatus.Draft,
                    CurrentStepId = 1,
                    CreatedBy = "USER02",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER01");
            var dto = new SavePortalRequestDto { Title = "Hacked Title" };

            var response = await client.PutAsJsonAsync($"/api/Portal/Requests/{requestId}", dto);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task SubmitPortalRequest_WhenMissingOriginalQuotation_Returns400BadRequest()
        {
            int requestId = 903;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(requestId) != null) return;
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = "QC-20260804-903",
                    Title = "Complete Info No Quote",
                    VendorCode = "V001",
                    VendorName = "Acme",
                    ValidFrom = DateTime.Now,
                    ValidUntil = DateTime.Now.AddDays(10),
                    Status = (int)RequestStatus.Draft,
                    CurrentStepId = 1,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.PostAsync($"/api/Portal/Requests/{requestId}/submit", null);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SubmitPortalRequest_WhenValidAndOriginalQuotationPresent_SubmitsSuccessfully()
        {
            int requestId = 904;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(requestId) != null) return;
                var req = new Request
                {
                    Id = requestId,
                    Code = "QC-20260804-904",
                    Title = "Valid Submission",
                    VendorCode = "V001",
                    VendorName = "Acme Corp",
                    ValidFrom = DateTime.Now,
                    ValidUntil = DateTime.Now.AddDays(10),
                    Status = (int)RequestStatus.Draft,
                    CurrentStepId = 1,
                    CreatedBy = "USER01",
                    IsActive = true
                };
                req.ApprovalSteps.Add(new ApprovalStep { Id = 9041, Sequence = 1, StepName = "Submitter", Status = (int)RequestStatus.Draft });
                req.ApprovalSteps.Add(new ApprovalStep { Id = 9042, Sequence = 2, StepName = "Manager", Status = (int)RequestStatus.Draft });
                req.Quotations.Add(new Quotation { Id = 90401, FileName = "orig_quote.pdf", FilePath = "/files/90401.pdf", DocumentTypeId = 10, ContentType = "application/pdf" });
                db.Requests.Add(req);
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.PostAsync($"/api/Portal/Requests/{requestId}/submit", null);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var detailResponse = await client.GetAsync($"/api/Portal/Requests/{requestId}");
            var detail = await detailResponse.Content.ReadFromJsonAsync<PortalRequestDetailDto>();
            detail.ShouldNotBeNull();
            detail.Status.ShouldBe((int)RequestStatus.Pending);
        }

        [Fact]
        public async Task DeletePortalDraft_WhenOwner_DeletesSuccessfully()
        {
            int requestId = 905;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(requestId) != null) return;
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = "QC-20260804-905",
                    Title = "Draft To Delete",
                    VendorCode = "V000",
                    VendorName = "Initial Vendor",
                    Status = (int)RequestStatus.Draft,
                    CurrentStepId = 1,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.DeleteAsync($"/api/Portal/Requests/{requestId}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DeletePortalAttachment_WhenBelongsToRequest_DeletesSuccessfully()
        {
            int requestId = 906;
            int quotationId = 90601;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(requestId) != null) return;
                var req = new Request
                {
                    Id = requestId,
                    Code = "QC-20260804-906",
                    Title = "Draft With Attachment",
                    VendorCode = "V000",
                    VendorName = "Initial Vendor",
                    Status = (int)RequestStatus.Draft,
                    CurrentStepId = 1,
                    CreatedBy = "USER01",
                    IsActive = true
                };
                req.Quotations.Add(new Quotation { Id = quotationId, FileName = "test_doc.pdf", FilePath = "/files/90601.pdf", DocumentTypeId = 10, ContentType = "application/pdf" });
                db.Requests.Add(req);
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.DeleteAsync($"/api/Portal/Requests/{requestId}/attachments/{quotationId}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DeletePortalAttachment_WhenCrossParentOrNotBelonging_Returns404NotFound()
        {
            int requestId1 = 907;
            int requestId2 = 908;
            int quotationId2 = 90801;

            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(requestId1) == null)
                {
                    db.Requests.Add(new Request
                    {
                        Id = requestId1,
                        Code = "QC-20260804-907",
                        Title = "Request 1",
                        VendorCode = "V000",
                        VendorName = "Vendor 1",
                        Status = (int)RequestStatus.Draft,
                        CreatedBy = "USER01",
                        IsActive = true
                    });
                }

                if (db.Requests.Find(requestId2) == null)
                {
                    var req2 = new Request
                    {
                        Id = requestId2,
                        Code = "QC-20260804-908",
                        Title = "Request 2",
                        VendorCode = "V000",
                        VendorName = "Vendor 2",
                        Status = (int)RequestStatus.Draft,
                        CreatedBy = "USER01",
                        IsActive = true
                    };
                    req2.Quotations.Add(new Quotation { Id = quotationId2, FileName = "req2_doc.pdf", FilePath = "/files/90801.pdf", DocumentTypeId = 10, ContentType = "application/pdf" });
                    db.Requests.Add(req2);
                }
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.DeleteAsync($"/api/Portal/Requests/{requestId1}/attachments/{quotationId2}");

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ApprovePortalRequest_WhenNotAssignedApprover_Returns403Forbidden()
        {
            int requestId = 999;
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);

                var req = new Request
                {
                    Id = requestId,
                    Code = "QC-20260804-999",
                    Title = "Pending Request",
                    VendorCode = "V000",
                    VendorName = "Vendor 999",
                    Status = (int)RequestStatus.Pending,
                    CurrentStepId = 2,
                    CreatedBy = "USER02",
                    IsActive = true
                };
                req.ApprovalSteps.Add(new ApprovalStep { Id = 9991, Sequence = 1, StepName = "Submitter", Status = (int)RequestStatus.Approved, ApproverNId = "USER02" });
                req.ApprovalSteps.Add(new ApprovalStep { Id = 9992, Sequence = 2, StepName = "Manager", Status = (int)RequestStatus.Pending, ApproverNId = "OTHER_USER" });
                db.Requests.Add(req);
            });

            var client = CreateAuthenticatedClient("USER01");
            var actionDto = new PortalApprovalActionDto { Comment = "Approving" };
            var response = await client.PostAsJsonAsync($"/api/Portal/Requests/{requestId}/approve", actionDto);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        /// <summary>
        /// An unrecognised document type must be rejected rather than stored. The codebase already
        /// applies this rule to RequestStatus and QcsRequestStatus: an unknown enum value throws,
        /// it never defaults.
        /// </summary>
        [Fact]
        public async Task UploadPortalAttachment_WhenDocumentTypeIdIsUndefined_Returns400BadRequest()
        {
            var response = await UploadAttachmentAsync(requestId: 920, code: "QC-20260804-920", documentTypeId: "77");

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Omitting the type used to default to Original Quotation - the one document type the
        /// submit rule requires - so a caller could satisfy that gate without ever choosing it.
        /// </summary>
        [Fact]
        public async Task UploadPortalAttachment_WhenDocumentTypeIdIsMissing_Returns400BadRequest()
        {
            var response = await UploadAttachmentAsync(requestId: 921, code: "QC-20260804-921", documentTypeId: null);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        private async Task<HttpResponseMessage> UploadAttachmentAsync(int requestId, string code, string? documentTypeId)
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(requestId) != null) return;
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = "Draft for attachment type validation",
                    VendorCode = "V000",
                    VendorName = "Initial Vendor",
                    Status = (int)RequestStatus.Draft,
                    CurrentStepId = 1,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var file = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 });
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

            using var form = new MultipartFormDataContent { { file, "file", "spec.pdf" } };
            if (documentTypeId is not null)
            {
                form.Add(new StringContent(documentTypeId), "documentTypeId");
            }

            var client = CreateAuthenticatedClient("USER01");
            return await client.PostAsync($"/api/Portal/Requests/{requestId}/attachments", form);
        }
    }
}
