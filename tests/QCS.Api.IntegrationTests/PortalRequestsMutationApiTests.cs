using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using QCS.Application.Abstractions;
using QCS.Domain.DTOs;
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
            _factory.SeedDatabase(db =>
            {
                var request = db.Requests.Find(result.Id);
                request.ShouldNotBeNull();
                request.CurrentStepSequence.ShouldBeNull();
                db.ApprovalSteps.ShouldNotContain(step => step.RequestId == result.Id);
            });
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
                    CurrentStepSequence = 1,
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
                    CurrentStepSequence = 1,
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
                    CurrentStepSequence = 1,
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
            _factory.ApprovalService.Reset();
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
                    CurrentStepSequence = 1,
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
            detail.Status.ShouldBe((int)RequestStatus.InProcess);
        }

        [Fact]
        public async Task SubmitPortalRequest_WhenApprovalServiceFails_LeavesLocalDraft()
        {
            _factory.ApprovalService.Reset();
            _factory.ApprovalService.CreateException = new HttpRequestException("Approval Service unavailable");
            int requestId = 909;
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);

                var request = new Request
                {
                    Id = requestId,
                    Code = "QC-20260804-909",
                    Title = "Submission must fail closed",
                    VendorCode = "V001",
                    VendorName = "Acme Corp",
                    ValidFrom = DateTime.Now,
                    ValidUntil = DateTime.Now.AddDays(10),
                    Status = (int)RequestStatus.Draft,
                    CreatedBy = "USER01",
                    IsActive = true
                };
                request.Quotations.Add(new Quotation
                {
                    Id = 90901,
                    FileName = "orig_quote.pdf",
                    FilePath = "/files/90901.pdf",
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    ContentType = "application/pdf"
                });
                db.Requests.Add(request);
            });

            try
            {
                var client = CreateAuthenticatedClient("USER01");
                var response = await client.PostAsync($"/api/Portal/Requests/{requestId}/submit", null);

                response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

                _factory.SeedDatabase(db =>
                {
                    var request = db.Requests.Find(requestId);
                    request.ShouldNotBeNull();
                    request.Status.ShouldBe((int)RequestStatus.Draft);
                    request.ApprovalDocumentId.ShouldBeNull();
                });
            }
            finally
            {
                _factory.ApprovalService.Reset();
            }
        }

        [Theory]
        [InlineData(RequestStatus.Draft, 1)]
        [InlineData(RequestStatus.InProcess, 0)]
        public async Task SubmitPortalRequest_WithExistingRemoteDocument_AdoptsWithoutDuplicateCreate(
            RequestStatus remoteStatus,
            int expectedSubmitCalls)
        {
            _factory.ApprovalService.Reset();
            int requestId = 950 + (int)remoteStatus;
            var documentId = Guid.NewGuid();
            var code = $"QC-20260804-{requestId}";
            _factory.ApprovalService.SeedDocument(code, documentId, "USER01", remoteStatus);
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);
                var request = new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = "Adopt remote document",
                    VendorCode = "V001",
                    VendorName = "Vendor",
                    ValidFrom = DateTime.Now,
                    ValidUntil = DateTime.Now.AddDays(10),
                    Status = (int)RequestStatus.Draft,
                    CreatedBy = "USER01",
                    IsActive = true
                };
                request.Quotations.Add(new Quotation
                {
                    Id = requestId * 100,
                    FileName = "original.pdf",
                    FilePath = $"/files/{requestId}.pdf",
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    ContentType = "application/pdf"
                });
                db.Requests.Add(request);
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.PostAsync($"/api/Portal/Requests/{requestId}/submit", null);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            _factory.ApprovalService.CreateCallCount.ShouldBe(0);
            _factory.ApprovalService.SubmittedDocumentIds.Count.ShouldBe(expectedSubmitCalls);
            _factory.SeedDatabase(db =>
            {
                var request = db.Requests.Find(requestId);
                request.ShouldNotBeNull();
                request.ApprovalDocumentId.ShouldBe(documentId);
                request.Status.ShouldBe((int)RequestStatus.InProcess);
            });
        }

        [Fact]
        public async Task DeletePortalDraft_WhenOwner_DeletesSuccessfully()
        {
            _factory.ApprovalService.Reset();
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
                    CurrentStepSequence = 1,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.DeleteAsync($"/api/Portal/Requests/{requestId}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DeletePortalDraft_WithCentralDraft_DeletesRemoteBeforeLocal()
        {
            _factory.ApprovalService.Reset();
            int requestId = 910;
            var documentId = Guid.NewGuid();
            const string code = "QC-20260804-910";
            _factory.ApprovalService.SeedDocument(code, documentId, "USER01");
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = "Central draft to delete",
                    VendorCode = "V000",
                    VendorName = "Initial Vendor",
                    Status = (int)RequestStatus.Draft,
                    ApprovalDocumentId = documentId,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.DeleteAsync($"/api/Portal/Requests/{requestId}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            _factory.ApprovalService.DeletedDraftIds.ShouldContain(documentId);
            _factory.SeedDatabase(db => db.Requests.Find(requestId).ShouldBeNull());
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
                    CurrentStepSequence = 1,
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
                        CurrentStepSequence = 1,
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
        public async Task GetPortalRequest_WithCentralDocument_WhenCallerIsNotParticipant_Returns403Forbidden()
        {
            _factory.ApprovalService.Reset();
            int requestId = 911;
            var documentId = Guid.NewGuid();
            const string code = "QC-20260804-911";
            _factory.ApprovalService.SeedDocument(
                code,
                documentId,
                "USER01",
                permissions: ApprovalPermissions.None);
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = "Private central request",
                    VendorCode = "V000",
                    VendorName = "Vendor",
                    Status = (int)RequestStatus.InProcess,
                    ApprovalDocumentId = documentId,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER02");
            var response = await client.GetAsync($"/api/Portal/Requests/{requestId}");

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetPortalRequest_WithStaleMirror_PersistsCentralStatusAndStep()
        {
            _factory.ApprovalService.Reset();
            int requestId = 912;
            var documentId = Guid.NewGuid();
            const string code = "QC-20260804-912";
            _factory.ApprovalService.SeedDocument(
                code,
                documentId,
                "USER01",
                RequestStatus.Completed,
                currentStepSequence: null,
                currentStepName: null);
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = "Stale central mirror",
                    VendorCode = "V000",
                    VendorName = "Vendor",
                    Status = (int)RequestStatus.InProcess,
                    CurrentStepSequence = 1,
                    CurrentStepName = "Old step",
                    ApprovalDocumentId = documentId,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.GetAsync($"/api/Portal/Requests/{requestId}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var detail = await response.Content.ReadFromJsonAsync<PortalRequestDetailDto>();
            detail.ShouldNotBeNull();
            detail.Status.ShouldBe((int)RequestStatus.Completed);
            detail.CurrentStepSequence.ShouldBeNull();

            _factory.SeedDatabase(db =>
            {
                var request = db.Requests.Find(requestId);
                request.ShouldNotBeNull();
                request.Status.ShouldBe((int)RequestStatus.Completed);
                request.CurrentStepSequence.ShouldBeNull();
                request.CurrentStepName.ShouldBeNull();
                request.StatusSyncedAt.ShouldNotBeNull();
            });
        }

        [Fact]
        public async Task GetAdminRequestDetail_WithFourCentralSteps_PreservesAllStepsAndAssignees()
        {
            _factory.ApprovalService.Reset();
            int requestId = 913;
            var documentId = Guid.NewGuid();
            const string code = "QC-20260804-913";
            var steps = Enumerable.Range(1, 4)
                .Select(sequence => new ApprovalStepView(
                    sequence,
                    $"Step {sequence}",
                    sequence == 1 ? "Completed" : "Pending",
                    IsFinalStep: sequence == 4,
                    Assignees: sequence == 2
                        ? new[]
                        {
                            new ApprovalAssigneeView("USER01", "User One", "Pending", null, null),
                            new ApprovalAssigneeView("USER02", "User Two", "Pending", null, null)
                        }
                        : Array.Empty<ApprovalAssigneeView>()))
                .ToArray();
            _factory.ApprovalService.SeedDocument(
                code,
                documentId,
                "USER01",
                RequestStatus.InProcess,
                steps: steps);
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = "Dynamic central route",
                    VendorCode = "V000",
                    VendorName = "Vendor",
                    Status = (int)RequestStatus.InProcess,
                    ApprovalDocumentId = documentId,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.GetAsync($"/api/Request/Detail/{requestId}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var detail = await response.Content.ReadFromJsonAsync<RequestDetailDto>();
            detail.ShouldNotBeNull();
            detail.WorkflowRoute.ShouldNotBeNull();
            detail.WorkflowRoute.Steps.Count.ShouldBe(4);
            detail.WorkflowRoute.Steps[1].Assignments.Count.ShouldBe(2);
        }

        [Fact]
        public async Task ApprovePortalRequest_WhenNotAssignedApprover_Returns403Forbidden()
        {
            _factory.ApprovalService.Reset();
            int requestId = 999;
            var documentId = Guid.NewGuid();
            const string code = "QC-20260804-999";
            _factory.ApprovalService.SeedDocument(code, documentId, "USER02", RequestStatus.InProcess);
            _factory.ApprovalService.ActionException = new UnauthorizedAccessException("Not an assignee");
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);

                var req = new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = "Pending Request",
                    VendorCode = "V000",
                    VendorName = "Vendor 999",
                    Status = (int)RequestStatus.InProcess,
                    CurrentStepSequence = 2,
                    ApprovalDocumentId = documentId,
                    CreatedBy = "USER02",
                    IsActive = true
                };
                db.Requests.Add(req);
            });

            try
            {
                var client = CreateAuthenticatedClient("USER01");
                var actionDto = new PortalApprovalActionDto { Comment = "Approving" };
                var response = await client.PostAsJsonAsync($"/api/Portal/Requests/{requestId}/approve", actionDto);

                response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            }
            finally
            {
                _factory.ApprovalService.Reset();
            }
        }

        [Theory]
        [InlineData("approve", RequestStatus.Completed)]
        [InlineData("reject", RequestStatus.Rejected)]
        [InlineData("return", RequestStatus.Returned)]
        [InlineData("cancel", RequestStatus.Cancelled)]
        public async Task CentralAction_RefreshesLocalMirror(string action, RequestStatus expectedStatus)
        {
            _factory.ApprovalService.Reset();
            int requestId = 930 + (int)expectedStatus;
            var documentId = Guid.NewGuid();
            var code = $"QC-20260804-{requestId}";
            _factory.ApprovalService.SeedDocument(code, documentId, "USER01", RequestStatus.InProcess);
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = $"Central {action}",
                    VendorCode = "V000",
                    VendorName = "Vendor",
                    Status = (int)RequestStatus.InProcess,
                    CurrentStepSequence = 2,
                    ApprovalDocumentId = documentId,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER01");
            var payload = new PortalApprovalActionDto
            {
                Comment = "Test central action",
                ReturnToStepSequence = action == "return" ? 1 : null
            };
            var response = await client.PostAsJsonAsync($"/api/Portal/Requests/{requestId}/{action}", payload);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            _factory.SeedDatabase(db =>
            {
                var request = db.Requests.Find(requestId);
                request.ShouldNotBeNull();
                request.Status.ShouldBe((int)expectedStatus);
                request.CurrentStepSequence.ShouldBeNull();
                request.StatusSyncedAt.ShouldNotBeNull();
            });
        }

        [Theory]
        [InlineData("reject")]
        [InlineData("return")]
        [InlineData("cancel")]
        public async Task CentralAction_WhenRequiredCommentIsMissing_Returns400BadRequest(string action)
        {
            _factory.ApprovalService.Reset();
            int requestId = 970 + action.Length;
            var documentId = Guid.NewGuid();
            var code = $"QC-20260804-{requestId}";
            _factory.ApprovalService.SeedDocument(code, documentId, "USER01", RequestStatus.InProcess);
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);
                db.Requests.Add(new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = $"Missing {action} comment",
                    VendorCode = "V000",
                    VendorName = "Vendor",
                    Status = (int)RequestStatus.InProcess,
                    ApprovalDocumentId = documentId,
                    CreatedBy = "USER01",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient("USER01");
            var response = await client.PostAsJsonAsync(
                $"/api/Portal/Requests/{requestId}/{action}",
                new PortalApprovalActionDto { Comment = " " });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
                    CurrentStepSequence = 1,
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
