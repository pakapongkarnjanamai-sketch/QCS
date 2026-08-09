using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Abstractions;
using QCS.Application.Services;
using QCS.Domain.DTOs.Integration;
using QCS.Domain.DTOs.Portal;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using Shouldly;
using Xunit;

namespace QCS.Api.IntegrationTests
{
    public class Phase1RenewalApiTests : IClassFixture<QcsApiFactory>
    {
        private readonly QcsApiFactory _factory;

        public Phase1RenewalApiTests(QcsApiFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateAuthenticatedClient(string userNid = "USER1")
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, $"NIKONOA\\{userNid}");
            return client;
        }

        private HttpClient CreateApiKeyClient(string apiKey = "test-api-key")
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            return client;
        }

        [Fact]
        public async Task GetRenewalCandidates_ReturnsAllUserEligibleCandidates_SortedByValidUntilAsc()
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == "QC-CAND-01")) return;

                var attachment = new AttachmentFile { Id = 8101, FileSize = 100, ContentType = "application/pdf", Data = "%PDF-1.4"u8.ToArray() };
                db.AttachmentFiles.Add(attachment);

                // Candidate 1: Expired (ValidUntil = 5 days ago), created by OTHER_USER
                var req1 = new Request
                {
                    Id = 8101,
                    Code = "QC-CAND-01",
                    Title = "Expired Candidate",
                    VendorCode = "V01",
                    VendorName = "Vendor 01",
                    Status = (int)RequestStatus.Completed,
                    ValidFrom = DateTime.Now.AddDays(-60),
                    ValidUntil = DateTime.Now.AddDays(-5),
                    CreatedBy = "OTHER_USER",
                    IsActive = true
                };
                req1.Quotations.Add(new Quotation { Id = 81010, DocumentTypeId = (int)DocumentType.OriginalQuotation, AttachmentFileId = attachment.Id, FileName = "orig.pdf", FilePath = "/files/8101.pdf", ContentType = "application/pdf" });
                db.Requests.Add(req1);

                // Candidate 2: Expiring Soon (ValidUntil = in 10 days), created by USER1
                var req2 = new Request
                {
                    Id = 8102,
                    Code = "QC-CAND-02",
                    Title = "Expiring Soon Candidate",
                    VendorCode = "V02",
                    VendorName = "Vendor 02",
                    Status = (int)RequestStatus.Completed,
                    ValidFrom = DateTime.Now.AddDays(-60),
                    ValidUntil = DateTime.Now.AddDays(10),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                req2.Quotations.Add(new Quotation { Id = 81020, DocumentTypeId = (int)DocumentType.OriginalQuotation, AttachmentFileId = attachment.Id, FileName = "orig.pdf", FilePath = "/files/8102.pdf", ContentType = "application/pdf" });
                db.Requests.Add(req2);
            });

            var client = CreateAuthenticatedClient("USER1");
            var response = await client.GetAsync("/api/Portal/Requests/renewal-candidates?page=1&pageSize=10");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var page = await response.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            page.ShouldNotBeNull();
            var items = page.Items;
            items.Any(i => i.Code == "QC-CAND-01").ShouldBeTrue();
            items.Any(i => i.Code == "QC-CAND-02").ShouldBeTrue();

            var cand1 = items.First(i => i.Code == "QC-CAND-01");
            cand1.RenewalWindowStatus.ShouldBe("Expired");

            var cand2 = items.First(i => i.Code == "QC-CAND-02");
            cand2.RenewalWindowStatus.ShouldBe("ExpiringSoon");

            // Verify order by ValidUntil ASC: QC-CAND-01 (expired -5d) should come before QC-CAND-02 (+10d)
            var itemList = items.ToList();
            var index1 = itemList.FindIndex(i => i.Code == "QC-CAND-01");
            var index2 = itemList.FindIndex(i => i.Code == "QC-CAND-02");
            index1.ShouldBeLessThan(index2);
        }

        [Fact]
        public async Task GetPortalRequestById_ComputesCanRenewCorrectly()
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == "QC-CANRENEW-01")) return;

                var attachment = new AttachmentFile { Id = 8201, FileSize = 100, ContentType = "application/pdf", Data = "%PDF-1.4"u8.ToArray() };
                db.AttachmentFiles.Add(attachment);

                var req = new Request
                {
                    Id = 8201,
                    Code = "QC-CANRENEW-01",
                    Title = "Completed Can Renew",
                    VendorCode = "V82",
                    VendorName = "Vendor 82",
                    Status = (int)RequestStatus.Completed,
                    ValidFrom = DateTime.Now.AddDays(-60),
                    ValidUntil = DateTime.Now.AddDays(-1),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                req.Quotations.Add(new Quotation { Id = 82010, DocumentTypeId = (int)DocumentType.OriginalQuotation, AttachmentFileId = attachment.Id, FileName = "orig.pdf", FilePath = "/files/8201.pdf", ContentType = "application/pdf" });
                db.Requests.Add(req);
            });

            var client = CreateAuthenticatedClient("USER1");
            var response = await client.GetAsync("/api/Portal/Requests/8201");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var detail = await response.Content.ReadFromJsonAsync<PortalRequestDetailDto>();
            detail.ShouldNotBeNull();
            detail.CanRenew.ShouldBeTrue();
        }

        [Fact]
        public async Task Integration_GetRenewalCandidates_ReturnsCandidatesUnderApiKeyPolicy()
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == "QC-INT-01")) return;

                var attachment = new AttachmentFile { Id = 8301, FileSize = 100, ContentType = "application/pdf", Data = "%PDF-1.4"u8.ToArray() };
                db.AttachmentFiles.Add(attachment);

                var req = new Request
                {
                    Id = 8301,
                    Code = "QC-INT-01",
                    Title = "Integration Candidate",
                    VendorCode = "V83",
                    VendorName = "Vendor 83",
                    Status = (int)RequestStatus.Completed,
                    ValidFrom = DateTime.Now.AddDays(-60),
                    ValidUntil = DateTime.Now.AddDays(-1),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                req.Quotations.Add(new Quotation { Id = 83010, DocumentTypeId = (int)DocumentType.OriginalQuotation, AttachmentFileId = attachment.Id, FileName = "orig.pdf", FilePath = "/files/8301.pdf", ContentType = "application/pdf" });
                db.Requests.Add(req);
            });

            var client = CreateApiKeyClient("test-api-key");
            var response = await client.GetAsync("/api/Integration/RenewalCandidates?search=QC-INT-01");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var page = await response.Content.ReadFromJsonAsync<PortalPage<IntegrationRenewalCandidateDto>>();
            page.ShouldNotBeNull();
            page.Items.Count.ShouldBe(1);
            page.Items[0].Code.ShouldBe("QC-INT-01");

            var byCodeResponse = await client.GetAsync("/api/Integration/RenewalCandidates/QC-INT-01");
            byCodeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var single = await byCodeResponse.Content.ReadFromJsonAsync<IntegrationRenewalCandidateDto>();
            single.ShouldNotBeNull();
            single.Code.ShouldBe("QC-INT-01");
        }

        [Fact]
        public async Task Integration_GetRenewalCandidates_WithoutApiKey_Returns401()
        {
            var client = CreateAuthenticatedClient();

            var response = await client.GetAsync("/api/Integration/RenewalCandidates");

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ResolveSetupFromQcs_ReturnsPredecessorSetup()
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == "QC-SET-01")) return;

                var attachment = new AttachmentFile { Id = 8401, FileSize = 100, ContentType = "application/pdf", Data = "%PDF-1.4"u8.ToArray() };
                db.AttachmentFiles.Add(attachment);

                var req = new Request
                {
                    Id = 8401,
                    Code = "QC-SET-01",
                    Title = "QCS Setup Source",
                    VendorCode = "V84",
                    VendorName = "Vendor 84",
                    Status = (int)RequestStatus.Completed,
                    ValidFrom = DateTime.Now.AddDays(-60),
                    ValidUntil = DateTime.Now.AddDays(-1),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                req.Quotations.Add(new Quotation { Id = 84010, DocumentTypeId = (int)DocumentType.OriginalQuotation, AttachmentFileId = attachment.Id, FileName = "orig.pdf", FilePath = "/files/8401.pdf", ContentType = "application/pdf" });
                db.Requests.Add(req);
            });

            var client = CreateAuthenticatedClient("USER1");
            var response = await client.GetAsync("/api/Portal/Requests/setup/from-qcs/QC-SET-01");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var setup = await response.Content.ReadFromJsonAsync<PortalSetupResolutionDto>();
            setup.ShouldNotBeNull();
            setup.Flow.ShouldBe("RenewalQcs");
            setup.Intent.ShouldBe(1);
            setup.Origin.ShouldBe("QCS");
            setup.RenewedFromRequestId.ShouldBe(8401);
            setup.RenewedFromCode.ShouldBe("QC-SET-01");
            setup.VendorCode.ShouldBe("V84");
            setup.VendorName.ShouldBe("Vendor 84");
        }

        [Fact]
        public async Task RenewalCandidate_PathOnlyOriginal_IsNotEligible()
        {
            const int requestId = 8501;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(requestId) != null) return;

                var request = new Request
                {
                    Id = requestId,
                    Code = "QC-PATH-ONLY-01",
                    Title = "Path-only original",
                    VendorCode = "V85",
                    VendorName = "Vendor 85",
                    Status = (int)RequestStatus.Completed,
                    ValidUntil = DateTime.Now.AddDays(10),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                request.Quotations.Add(new Quotation
                {
                    Id = 85010,
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    FileName = "legacy.pdf",
                    FilePath = "/legacy/path-only.pdf",
                    ContentType = "application/pdf"
                });
                db.Requests.Add(request);
            });

            var client = CreateAuthenticatedClient();
            var listResponse = await client.GetAsync(
                "/api/Portal/Requests/renewal-candidates?search=QC-PATH-ONLY-01&page=1&pageSize=10");

            listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var page = await listResponse.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            page.ShouldNotBeNull();
            page.Items.ShouldNotContain(item => item.Code == "QC-PATH-ONLY-01");

            var resolveResponse = await client.GetAsync(
                "/api/Portal/Requests/setup/from-qcs/QC-PATH-ONLY-01");
            resolveResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task RenewalCandidate_OutsideThirtyDayWindow_IsNotEligible()
        {
            const int requestId = 8551;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(requestId) != null) return;

                var request = new Request
                {
                    Id = requestId,
                    Code = "QC-OUTSIDE-WINDOW-01",
                    Title = "Outside renewal window",
                    VendorCode = "V855",
                    VendorName = "Vendor 855",
                    Status = (int)RequestStatus.Completed,
                    ValidUntil = DateTime.Now.AddDays(31),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                request.Quotations.Add(new Quotation
                {
                    Id = 85510,
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    FileName = "original.pdf",
                    FilePath = "/files/8551.pdf",
                    ContentType = "application/pdf",
                    AttachmentFile = CreateUploadedPdf()
                });
                db.Requests.Add(request);
            });

            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync(
                "/api/Portal/Requests/renewal-candidates?search=QC-OUTSIDE-WINDOW-01&page=1&pageSize=10");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var page = await response.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            page.ShouldNotBeNull();
            page.Items.ShouldNotContain(item => item.Code == "QC-OUTSIDE-WINDOW-01");
        }

        [Fact]
        public async Task RenewalCandidate_ThirtyDayBoundary_IsInclusiveAndOneTickBeyondIsExcluded()
        {
            var previousNow = _factory.DateTime.Now;
            var fixedNow = new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Local);
            _factory.DateTime.Now = fixedNow;
            try
            {
                _factory.SeedDatabase(db =>
                {
                    if (db.Requests.Any(request => request.Code == "QC-BOUNDARY-30")) return;

                    db.Requests.Add(CreateEligibleRequest(
                        8561,
                        "QC-BOUNDARY-30",
                        fixedNow.AddDays(30)));
                    db.Requests.Add(CreateEligibleRequest(
                        8562,
                        "QC-BOUNDARY-BEYOND",
                        fixedNow.AddDays(30).AddTicks(1)));
                });

                var client = CreateAuthenticatedClient();
                var includedResponse = await client.GetAsync(
                    "/api/Portal/Requests/renewal-candidates?search=QC-BOUNDARY-30&page=1&pageSize=10");
                var includedPage = await includedResponse.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
                includedPage.ShouldNotBeNull();
                includedPage.Items.ShouldContain(item => item.Code == "QC-BOUNDARY-30");

                var excludedResponse = await client.GetAsync(
                    "/api/Portal/Requests/renewal-candidates?search=QC-BOUNDARY-BEYOND&page=1&pageSize=10");
                var excludedPage = await excludedResponse.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
                excludedPage.ShouldNotBeNull();
                excludedPage.Items.ShouldNotContain(item => item.Code == "QC-BOUNDARY-BEYOND");
            }
            finally
            {
                _factory.DateTime.Now = previousNow;
            }
        }

        [Fact]
        public async Task RenewalCandidate_OriginalThatIsItselfAReference_IsNotEligible()
        {
            const int sourceRequestId = 8581;
            const int candidateRequestId = 8582;
            const int sourceQuotationId = 85810;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(candidateRequestId) != null) return;

                var source = CreateEligibleRequest(
                    sourceRequestId,
                    "QC-REFERENCE-SOURCE",
                    DateTime.Now.AddDays(10));
                source.Quotations.Single().Id = sourceQuotationId;
                db.Requests.Add(source);

                var candidate = new Request
                {
                    Id = candidateRequestId,
                    Code = "QC-REFERENCE-AS-ORIGINAL",
                    Title = "Reference typed as original",
                    VendorCode = "V858",
                    VendorName = "Vendor 858",
                    Status = (int)RequestStatus.Completed,
                    ValidUntil = DateTime.Now.AddDays(10),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                candidate.Quotations.Add(new Quotation
                {
                    Id = 85820,
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    SourceQuotationId = sourceQuotationId,
                    FileName = "reference.pdf",
                    FilePath = "Reference",
                    ContentType = "application/pdf",
                    AttachmentFile = CreateUploadedPdf()
                });
                db.Requests.Add(candidate);
            });

            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync(
                "/api/Portal/Requests/renewal-candidates?search=QC-REFERENCE-AS-ORIGINAL&page=1&pageSize=10");

            var page = await response.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            page.ShouldNotBeNull();
            page.Items.ShouldNotContain(item => item.Code == "QC-REFERENCE-AS-ORIGINAL");
        }

        [Fact]
        public async Task ConsumedPredecessor_IsExcludedAndQrsResolverReturns409WithBothCodes()
        {
            const int predecessorId = 8571;
            const int successorId = 8572;
            const string predecessorCode = "QC-CONSUMED-01";
            const string qrsCode = "QRS-CONSUMED-01";

            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(predecessorId) != null) return;

                var predecessor = new Request
                {
                    Id = predecessorId,
                    Code = predecessorCode,
                    Title = "Consumed predecessor",
                    VendorCode = "V857",
                    VendorName = "Vendor 857",
                    Status = (int)RequestStatus.Completed,
                    ValidUntil = DateTime.Now.AddDays(10),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                predecessor.Quotations.Add(new Quotation
                {
                    Id = 85710,
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    FileName = "original.pdf",
                    FilePath = "/files/8571.pdf",
                    ContentType = "application/pdf",
                    AttachmentFile = CreateUploadedPdf()
                });
                db.Requests.Add(predecessor);
                db.Requests.Add(new Request
                {
                    Id = successorId,
                    Code = "QC-SUCCESSOR-01",
                    Title = "Existing successor",
                    Intent = RequestIntent.Renewal,
                    RenewedFromRequestId = predecessorId,
                    VendorCode = "V857",
                    VendorName = "Vendor 857",
                    Status = (int)RequestStatus.Draft,
                    CreatedBy = "USER2",
                    IsActive = true
                });
            });
            _factory.QrsSourcingService.SetDetail(qrsCode, new QrsSourcingDetailDto
            {
                Code = qrsCode,
                Title = "Approved renewal request",
                RequestType = (int)QrsRequestType.Goods,
                Intent = (int)QrsRequestIntent.Renewal,
                PreviousQcCode = predecessorCode
            });

            var client = CreateAuthenticatedClient();
            var listResponse = await client.GetAsync(
                $"/api/Portal/Requests/renewal-candidates?search={predecessorCode}&page=1&pageSize=10");
            var page = await listResponse.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            page.ShouldNotBeNull();
            page.Items.ShouldNotContain(item => item.Code == predecessorCode);

            var qcsResolveResponse = await client.GetAsync(
                $"/api/Portal/Requests/setup/from-qcs/{predecessorCode}");
            qcsResolveResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            var qrsResolveResponse = await client.GetAsync(
                $"/api/Portal/Requests/setup/from-qrs/{qrsCode}");
            qrsResolveResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            var problem = await qrsResolveResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            problem.ShouldNotBeNull();
            problem.Extensions["qrsCode"].ShouldBeOfType<JsonElement>().GetString().ShouldBe(qrsCode);
            problem.Extensions["previousQcCode"].ShouldBeOfType<JsonElement>().GetString().ShouldBe(predecessorCode);
        }

        [Fact]
        public async Task ResolveAndCreateFromQrs_UnknownRequestType_AreRejected()
        {
            const string qrsCode = "QRS-UNKNOWN-TYPE-01";
            _factory.QrsSourcingService.SetDetail(qrsCode, new QrsSourcingDetailDto
            {
                Code = qrsCode,
                Title = "Unknown QRS request type",
                RequestType = 99,
                Intent = (int)QrsRequestIntent.Renewal,
                PreviousQcCode = "QC-DOES-NOT-MATTER"
            });

            // Contract C: an unreadable upstream type/intent is 502, not 400. The caller
            // did nothing wrong — QRS sent a value this version cannot interpret — and a
            // 400 would send the user off editing a request that is not the problem.
            var client = CreateAuthenticatedClient();
            var resolveResponse = await client.GetAsync(
                $"/api/Portal/Requests/setup/from-qrs/{qrsCode}");
            resolveResponse.StatusCode.ShouldBe(HttpStatusCode.BadGateway);

            var resolveProblem = await resolveResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            resolveProblem.ShouldNotBeNull();
            resolveProblem.Detail.ShouldBeNull();          // no upstream body, no parser detail
            (resolveProblem.Title ?? string.Empty).ShouldNotContain("99");

            var createResponse = await client.PostAsJsonAsync("/api/Portal/Requests", new SavePortalRequestDto
            {
                Intent = RequestIntent.Renewal,
                SourceSystem = "QRS",
                SourceCode = qrsCode,
                Title = "Must not be created"
            });
            createResponse.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        }

        [Fact]
        public async Task CreateRenewalFromQrs_WhenQrsIsUnavailable_Returns503WithoutLeakingDetail()
        {
            const string qrsCode = "QRS-UNAVAILABLE-01";
            _factory.QrsSourcingService.SetFailure(
                qrsCode,
                new QrsSourcingException("Sensitive upstream failure", StatusCodes.Status503ServiceUnavailable));
            var client = CreateAuthenticatedClient();

            var response = await client.PostAsJsonAsync("/api/Portal/Requests", new SavePortalRequestDto
            {
                Intent = RequestIntent.Renewal,
                SourceSystem = "QRS",
                SourceCode = qrsCode,
                Title = "Must fail closed"
            });

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            problem.ShouldNotBeNull();
            problem.Title.ShouldBe("QRS sourcing lookup is unavailable.");
            problem.Detail.ShouldBeNull();
        }

        [Fact]
        public async Task CreateRenewalFromQrs_IgnoresTamperedPredecessorAndVendor()
        {
            const int predecessorId = 8591;
            const string predecessorCode = "QC-QRS-AUTHORITY-01";
            const string qrsCode = "QRS-QCS-AUTHORITY-01";
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(predecessorId) != null) return;
                db.Requests.Add(CreateEligibleRequest(
                    predecessorId,
                    predecessorCode,
                    DateTime.Now.AddDays(10),
                    vendorCode: "V859",
                    vendorName: "Authoritative Vendor"));
            });
            _factory.QrsSourcingService.SetDetail(qrsCode, new QrsSourcingDetailDto
            {
                Code = qrsCode,
                Title = "Renewal from QRS",
                RequestType = (int)QrsRequestType.Medicine,
                Intent = (int)QrsRequestIntent.Renewal,
                PreviousQcCode = predecessorCode
            });

            var client = CreateAuthenticatedClient();
            var response = await client.PostAsJsonAsync("/api/Portal/Requests", new SavePortalRequestDto
            {
                Intent = RequestIntent.Renewal,
                SourceSystem = "QRS",
                SourceCode = qrsCode,
                RenewedFromRequestId = 999999,
                VendorCode = "TAMPERED",
                VendorName = "Tampered Vendor",
                Title = "Server-owned setup"
            });

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<PortalSaveResultDto>();
            result.ShouldNotBeNull();
            _factory.SeedDatabase(db =>
            {
                var created = db.Requests.Single(request => request.Id == result.Id);
                created.RenewedFromRequestId.ShouldBe(predecessorId);
                created.VendorCode.ShouldBe("V859");
                created.VendorName.ShouldBe("Authoritative Vendor");
            });
        }

        [Fact]
        public async Task ResolveAndCreateFromQrs_WhenQrsContractIsInvalid_Return502()
        {
            const string qrsCode = "QRS-INVALID-CONTRACT-01";
            _factory.QrsSourcingService.SetFailure(
                qrsCode,
                new QrsSourcingException("Sensitive contract payload", isContractViolation: true));
            var client = CreateAuthenticatedClient();

            var resolveResponse = await client.GetAsync(
                $"/api/Portal/Requests/setup/from-qrs/{qrsCode}");
            resolveResponse.StatusCode.ShouldBe(HttpStatusCode.BadGateway);

            var createResponse = await client.PostAsJsonAsync("/api/Portal/Requests", new SavePortalRequestDto
            {
                Intent = RequestIntent.Renewal,
                SourceSystem = "QRS",
                SourceCode = qrsCode,
                Title = "Must reject invalid upstream contract"
            });
            createResponse.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
            var problem = await createResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            problem.ShouldNotBeNull();
            problem.Title.ShouldBe("QRS sourcing response is invalid.");
            problem.Detail.ShouldBeNull();
        }

        [Fact]
        public async Task CreateRenewal_ExpiringSoonPredecessor_AddsAutomaticReference()
        {
            const int predecessorId = 8601;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(predecessorId) != null) return;

                var attachment = new AttachmentFile
                {
                    Id = 8601,
                    FileSize = 100,
                    ContentType = "application/pdf",
                    Data = "%PDF-1.4"u8.ToArray()
                };
                db.AttachmentFiles.Add(attachment);

                var predecessor = new Request
                {
                    Id = predecessorId,
                    Code = "QC-EXPIRING-01",
                    Title = "Expiring soon predecessor",
                    VendorCode = "V86",
                    VendorName = "Vendor 86",
                    Status = (int)RequestStatus.Completed,
                    ValidUntil = DateTime.Now.AddDays(10),
                    CreatedBy = "OTHER_USER",
                    IsActive = true
                };
                predecessor.Quotations.Add(new Quotation
                {
                    Id = 86010,
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    AttachmentFileId = attachment.Id,
                    FileName = "original.pdf",
                    FilePath = "/files/8601.pdf",
                    ContentType = "application/pdf",
                    SortOrder = 1
                });
                db.Requests.Add(predecessor);
            });

            var client = CreateAuthenticatedClient("USER1");
            var response = await client.PostAsJsonAsync("/api/Portal/Requests", new SavePortalRequestDto
            {
                Intent = RequestIntent.Renewal,
                RenewedFromRequestId = predecessorId,
                Title = "Renew before expiry"
            });

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<PortalSaveResultDto>();
            result.ShouldNotBeNull();

            _factory.SeedDatabase(db =>
            {
                var created = db.Requests.Include(request => request.Quotations)
                    .Single(request => request.Id == result.Id);
                created.RenewedFromRequestId.ShouldBe(predecessorId);
                created.Quotations.ShouldContain(quotation =>
                    quotation.DocumentTypeId == (int)DocumentType.ExpiredQuotation
                    && quotation.SourceQuotationId == 86010);
            });
        }

        private static AttachmentFile CreateUploadedPdf() => new()
        {
            ContentType = "application/pdf",
            FileSize = 8,
            Data = "%PDF-1.4"u8.ToArray()
        };

        private static Request CreateEligibleRequest(
            int id,
            string code,
            DateTime validUntil,
            string vendorCode = "V856",
            string vendorName = "Vendor 856")
        {
            var request = new Request
            {
                Id = id,
                Code = code,
                Title = $"Eligible {code}",
                VendorCode = vendorCode,
                VendorName = vendorName,
                Status = (int)RequestStatus.Completed,
                ValidUntil = validUntil,
                CreatedBy = "USER1",
                IsActive = true
            };
            request.Quotations.Add(new Quotation
            {
                FileName = "original.pdf",
                FilePath = $"/files/{id}.pdf",
                DocumentTypeId = (int)DocumentType.OriginalQuotation,
                ContentType = "application/pdf",
                AttachmentFile = CreateUploadedPdf()
            });
            return request;
        }

        [Fact]
        public async Task RenewalCandidate_NonCompletedRows_AreExcludedFromCandidates()
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == "QC-NONCOMP-DRAFT")) return;

                db.Requests.Add(new Request
                {
                    Id = 8801,
                    Code = "QC-NONCOMP-DRAFT",
                    Title = "Draft NonCompleted",
                    VendorCode = "V8801",
                    VendorName = "Vendor 8801",
                    Status = (int)RequestStatus.Draft,
                    ValidFrom = DateTime.Now.AddDays(-60),
                    ValidUntil = DateTime.Now.AddDays(10),
                    CreatedBy = "USER1",
                    IsActive = true,
                    Quotations = { new Quotation { FileName = "orig.pdf", FilePath = "/files/8801.pdf", DocumentTypeId = (int)DocumentType.OriginalQuotation, ContentType = "application/pdf", AttachmentFile = CreateUploadedPdf() } }
                });
                db.Requests.Add(new Request
                {
                    Id = 8802,
                    Code = "QC-NONCOMP-INPROC",
                    Title = "InProcess NonCompleted",
                    VendorCode = "V8802",
                    VendorName = "Vendor 8802",
                    Status = (int)RequestStatus.InProcess,
                    ValidFrom = DateTime.Now.AddDays(-60),
                    ValidUntil = DateTime.Now.AddDays(10),
                    CreatedBy = "USER1",
                    IsActive = true,
                    Quotations = { new Quotation { FileName = "orig.pdf", FilePath = "/files/8802.pdf", DocumentTypeId = (int)DocumentType.OriginalQuotation, ContentType = "application/pdf", AttachmentFile = CreateUploadedPdf() } }
                });
                db.Requests.Add(new Request
                {
                    Id = 8803,
                    Code = "QC-NONCOMP-CANCEL",
                    Title = "Cancelled NonCompleted",
                    VendorCode = "V8803",
                    VendorName = "Vendor 8803",
                    Status = (int)RequestStatus.Cancelled,
                    ValidFrom = DateTime.Now.AddDays(-60),
                    ValidUntil = DateTime.Now.AddDays(10),
                    CreatedBy = "USER1",
                    IsActive = true,
                    Quotations = { new Quotation { FileName = "orig.pdf", FilePath = "/files/8803.pdf", DocumentTypeId = (int)DocumentType.OriginalQuotation, ContentType = "application/pdf", AttachmentFile = CreateUploadedPdf() } }
                });
            });

            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/Portal/Requests/renewal-candidates?page=1&pageSize=100");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var page = await response.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            page.ShouldNotBeNull();
            page.Items.ShouldNotContain(item => item.Code == "QC-NONCOMP-DRAFT");
            page.Items.ShouldNotContain(item => item.Code == "QC-NONCOMP-INPROC");
            page.Items.ShouldNotContain(item => item.Code == "QC-NONCOMP-CANCEL");
        }

        [Fact]
        public async Task RenewalCandidate_NullValidUntil_IsExcluded()
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == "QC-NULL-VALIDUNTIL")) return;

                db.Requests.Add(new Request
                {
                    Id = 8810,
                    Code = "QC-NULL-VALIDUNTIL",
                    Title = "Null ValidUntil Candidate",
                    VendorCode = "V8810",
                    VendorName = "Vendor 8810",
                    Status = (int)RequestStatus.Completed,
                    ValidFrom = DateTime.Now.AddDays(-60),
                    ValidUntil = null,
                    CreatedBy = "USER1",
                    IsActive = true,
                    Quotations = { new Quotation { FileName = "orig.pdf", FilePath = "/files/8810.pdf", DocumentTypeId = (int)DocumentType.OriginalQuotation, ContentType = "application/pdf", AttachmentFile = CreateUploadedPdf() } }
                });
            });

            var client = CreateAuthenticatedClient();
            var response = await client.GetAsync("/api/Portal/Requests/renewal-candidates?page=1&pageSize=100");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var page = await response.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            page.ShouldNotBeNull();
            page.Items.ShouldNotContain(item => item.Code == "QC-NULL-VALIDUNTIL");
        }

        [Fact]
        public async Task RenewalCandidate_CancelledOrRejectedChild_StillConsumesPredecessor()
        {
            const int p1Id = 8821;
            const int c1Id = 8822;
            const int p2Id = 8823;
            const int c2Id = 8824;
            const string p1Code = "QC-PRED-CANCELLED-CHILD";
            const string p2Code = "QC-PRED-REJECTED-CHILD";

            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(p1Id) != null) return;

                db.Requests.Add(CreateEligibleRequest(p1Id, p1Code, DateTime.Now.AddDays(10)));
                db.Requests.Add(new Request
                {
                    Id = c1Id,
                    Code = "QC-CHILD-CANCELLED",
                    Title = "Cancelled Child",
                    Intent = RequestIntent.Renewal,
                    RenewedFromRequestId = p1Id,
                    VendorCode = "V856",
                    VendorName = "Vendor 856",
                    Status = (int)RequestStatus.Cancelled,
                    CreatedBy = "USER1",
                    IsActive = true
                });

                db.Requests.Add(CreateEligibleRequest(p2Id, p2Code, DateTime.Now.AddDays(10)));
                db.Requests.Add(new Request
                {
                    Id = c2Id,
                    Code = "QC-CHILD-REJECTED",
                    Title = "Rejected Child",
                    Intent = RequestIntent.Renewal,
                    RenewedFromRequestId = p2Id,
                    VendorCode = "V856",
                    VendorName = "Vendor 856",
                    Status = (int)RequestStatus.Rejected,
                    CreatedBy = "USER1",
                    IsActive = true
                });
            });

            var client = CreateAuthenticatedClient();
            var listResponse = await client.GetAsync("/api/Portal/Requests/renewal-candidates?page=1&pageSize=100");
            var page = await listResponse.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            page.ShouldNotBeNull();
            page.Items.ShouldNotContain(item => item.Code == p1Code);
            page.Items.ShouldNotContain(item => item.Code == p2Code);

            var resolve1Response = await client.GetAsync($"/api/Portal/Requests/setup/from-qcs/{p1Code}");
            resolve1Response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            var resolve2Response = await client.GetAsync($"/api/Portal/Requests/setup/from-qcs/{p2Code}");
            resolve2Response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Integration_GetRenewalCandidateByCode_NonExistentAndIneligible_BothReturn404()
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == "QC-INELIGIBLE-DRAFT")) return;

                db.Requests.Add(new Request
                {
                    Id = 8831,
                    Code = "QC-INELIGIBLE-DRAFT",
                    Title = "Ineligible Draft",
                    VendorCode = "V8831",
                    VendorName = "Vendor 8831",
                    Status = (int)RequestStatus.Draft,
                    ValidFrom = DateTime.Now.AddDays(-60),
                    ValidUntil = DateTime.Now.AddDays(10),
                    CreatedBy = "USER1",
                    IsActive = true,
                    Quotations = { new Quotation { FileName = "orig.pdf", FilePath = "/files/8831.pdf", DocumentTypeId = (int)DocumentType.OriginalQuotation, ContentType = "application/pdf", AttachmentFile = CreateUploadedPdf() } }
                });
            });

            var client = CreateApiKeyClient();

            var nonExistentResponse = await client.GetAsync("/api/Integration/RenewalCandidates/QC-NONEXISTENT-999");
            nonExistentResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            var ineligibleResponse = await client.GetAsync("/api/Integration/RenewalCandidates/QC-INELIGIBLE-DRAFT");
            ineligibleResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Integration_GetRenewalCandidates_PageSizeCappedAt100AndDefaultsTo10()
        {
            var client = CreateApiKeyClient();

            var cappedResponse = await client.GetAsync("/api/Integration/RenewalCandidates?pageSize=500");
            cappedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var cappedPage = await cappedResponse.Content.ReadFromJsonAsync<PortalPage<IntegrationRenewalCandidateDto>>();
            cappedPage.ShouldNotBeNull();
            cappedPage.PageSize.ShouldBe(100);

            var defaultResponse = await client.GetAsync("/api/Integration/RenewalCandidates");
            defaultResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var defaultPage = await defaultResponse.Content.ReadFromJsonAsync<PortalPage<IntegrationRenewalCandidateDto>>();
            defaultPage.ShouldNotBeNull();
            defaultPage.PageSize.ShouldBe(10);
        }

        [Fact]
        public async Task RenewalCandidate_PagingIsDeterministicAcrossPageBoundary()
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == "QC-PAGE-01")) return;

                db.Requests.Add(CreateEligibleRequest(8841, "QC-PAGE-01", DateTime.Now.AddDays(1)));
                db.Requests.Add(CreateEligibleRequest(8842, "QC-PAGE-02", DateTime.Now.AddDays(2)));
                db.Requests.Add(CreateEligibleRequest(8843, "QC-PAGE-03", DateTime.Now.AddDays(3)));
                db.Requests.Add(CreateEligibleRequest(8844, "QC-PAGE-04", DateTime.Now.AddDays(4)));
                db.Requests.Add(CreateEligibleRequest(8845, "QC-PAGE-05", DateTime.Now.AddDays(5)));
            });

            var client = CreateAuthenticatedClient();

            var p1Response = await client.GetAsync("/api/Portal/Requests/renewal-candidates?page=1&pageSize=2");
            var p1Page = await p1Response.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            p1Page.ShouldNotBeNull();
            p1Page.Items.Count.ShouldBe(2);

            var p2Response = await client.GetAsync("/api/Portal/Requests/renewal-candidates?page=2&pageSize=2");
            var p2Page = await p2Response.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            p2Page.ShouldNotBeNull();
            p2Page.Items.Count.ShouldBe(2);

            var p1Codes = p1Page.Items.Select(i => i.Code).ToList();
            var p2Codes = p2Page.Items.Select(i => i.Code).ToList();

            p1Codes.Intersect(p2Codes).ShouldBeEmpty();

            var combined = p1Page.Items.Concat(p2Page.Items).ToList();
            var sorted = combined.OrderBy(i => i.ValidUntil).ThenByDescending(i => i.Id).ToList();
            combined.Select(i => i.Id).ShouldBe(sorted.Select(i => i.Id));
        }

        [Fact]
        public async Task RenewalCandidate_PortalAndIntegration_ReturnSameCodesInSameOrder()
        {
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == "QC-DUAL-01")) return;

                db.Requests.Add(CreateEligibleRequest(8851, "QC-DUAL-01", DateTime.Now.AddDays(2)));
                db.Requests.Add(CreateEligibleRequest(8852, "QC-DUAL-02", DateTime.Now.AddDays(4)));
                db.Requests.Add(CreateEligibleRequest(8853, "QC-DUAL-03", DateTime.Now.AddDays(6)));
            });

            var portalClient = CreateAuthenticatedClient();
            var portalRes = await portalClient.GetAsync("/api/Portal/Requests/renewal-candidates?page=1&pageSize=100");
            var portalPage = await portalRes.Content.ReadFromJsonAsync<PortalPage<RenewalCandidateDto>>();
            portalPage.ShouldNotBeNull();

            var apiClient = CreateApiKeyClient();
            var apiRes = await apiClient.GetAsync("/api/Integration/RenewalCandidates?page=1&pageSize=100");
            var apiPage = await apiRes.Content.ReadFromJsonAsync<PortalPage<IntegrationRenewalCandidateDto>>();
            apiPage.ShouldNotBeNull();

            var portalCodes = portalPage.Items.Select(i => i.Code).ToList();
            var apiCodes = apiPage.Items.Select(i => i.Code).ToList();

            portalCodes.ShouldBe(apiCodes);
        }

        [Fact]
        public async Task ResolveSetupFromQrs_MissingCases()
        {
            // Case 1: New-intent QRS request resolves flow = NewQrs with null predecessor
            const string newQrsCode = "QRS-SETUP-NEW-01";
            _factory.QrsSourcingService.SetDetail(newQrsCode, new QrsSourcingDetailDto
            {
                Code = newQrsCode,
                Title = "New QRS Request",
                RequestType = (int)QrsRequestType.Goods,
                Intent = (int)QrsRequestIntent.New,
                PreviousQcCode = null
            });

            var client = CreateAuthenticatedClient();
            var newRes = await client.GetAsync($"/api/Portal/Requests/setup/from-qrs/{newQrsCode}");
            newRes.StatusCode.ShouldBe(HttpStatusCode.OK);
            var newSetup = await newRes.Content.ReadFromJsonAsync<PortalSetupResolutionDto>();
            newSetup.ShouldNotBeNull();
            newSetup.Flow.ShouldBe("NewQrs");
            newSetup.Intent.ShouldBe((int)RequestIntent.New);
            newSetup.RenewedFromRequestId.ShouldBeNull();
            newSetup.RenewedFromCode.ShouldBeNull();

            // Case 2: Renewal-intent QRS request on Medicine
            const string medPredecessorCode = "QC-MED-PRED-01";
            const string medQrsCode = "QRS-SETUP-MED-01";
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == medPredecessorCode)) return;
                db.Requests.Add(CreateEligibleRequest(8861, medPredecessorCode, DateTime.Now.AddDays(10)));
            });
            _factory.QrsSourcingService.SetDetail(medQrsCode, new QrsSourcingDetailDto
            {
                Code = medQrsCode,
                Title = "Medicine Renewal QRS",
                RequestType = (int)QrsRequestType.Medicine,
                Intent = (int)QrsRequestIntent.Renewal,
                PreviousQcCode = medPredecessorCode
            });

            var medRes = await client.GetAsync($"/api/Portal/Requests/setup/from-qrs/{medQrsCode}");
            medRes.StatusCode.ShouldBe(HttpStatusCode.OK);
            var medSetup = await medRes.Content.ReadFromJsonAsync<PortalSetupResolutionDto>();
            medSetup.ShouldNotBeNull();
            medSetup.Flow.ShouldBe("RenewalQrs");
            medSetup.Intent.ShouldBe((int)RequestIntent.Renewal);

            // Case 3: Renewal-intent QRS request with blank PreviousQcCode is rejected
            const string blankPrevCode = "QRS-SETUP-BLANK-PREV";
            _factory.QrsSourcingService.SetDetail(blankPrevCode, new QrsSourcingDetailDto
            {
                Code = blankPrevCode,
                Title = "Blank Previous Code QRS",
                RequestType = (int)QrsRequestType.Goods,
                Intent = (int)QrsRequestIntent.Renewal,
                PreviousQcCode = "   "
            });

            var blankRes = await client.GetAsync($"/api/Portal/Requests/setup/from-qrs/{blankPrevCode}");
            blankRes.StatusCode.ShouldNotBe(HttpStatusCode.OK);

            // Case 4: Upstream QRS 404 surfaces as 404
            const string missingQrsCode = "QRS-SETUP-404-01";
            _factory.QrsSourcingService.SetDetail(missingQrsCode, null!);

            var missingRes = await client.GetAsync($"/api/Portal/Requests/setup/from-qrs/{missingQrsCode}");
            missingRes.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task SubmitReadiness_ComparisonDocuments_Behavior()
        {
            var client = CreateAuthenticatedClient("USER1");

            // 1. One Original and 0 Comparison Documents -> succeeds
            const int req1Id = 8871;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(req1Id) != null) return;
                var req = new Request
                {
                    Id = req1Id,
                    Code = "QC-SUBMIT-0COMP",
                    Title = "Submit zero comparison",
                    VendorCode = "V8871",
                    VendorName = "Vendor 8871",
                    Status = (int)RequestStatus.Draft,
                    ValidFrom = DateTime.Now,
                    ValidUntil = DateTime.Now.AddDays(30),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                req.Quotations.Add(new Quotation
                {
                    FileName = "orig.pdf",
                    FilePath = "/files/8871.pdf",
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    ContentType = "application/pdf",
                    AttachmentFile = CreateUploadedPdf(),
                    SortOrder = 1
                });
                db.Requests.Add(req);
            });

            var submit1Res = await client.PostAsync($"/api/Portal/Requests/{req1Id}/submit", null);
            submit1Res.StatusCode.ShouldBe(HttpStatusCode.OK);

            // 2. One Original and 3 Comparison Documents -> succeeds and preserves order
            const int req2Id = 8872;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(req2Id) != null) return;
                var req = new Request
                {
                    Id = req2Id,
                    Code = "QC-SUBMIT-3COMP",
                    Title = "Submit three comparisons",
                    VendorCode = "V8872",
                    VendorName = "Vendor 8872",
                    Status = (int)RequestStatus.Draft,
                    ValidFrom = DateTime.Now,
                    ValidUntil = DateTime.Now.AddDays(30),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                req.Quotations.Add(new Quotation
                {
                    Id = 88720,
                    FileName = "orig.pdf",
                    FilePath = "/files/8872.pdf",
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    ContentType = "application/pdf",
                    AttachmentFile = CreateUploadedPdf(),
                    SortOrder = 1
                });
                req.Quotations.Add(new Quotation
                {
                    Id = 88721,
                    FileName = "comp1.pdf",
                    FilePath = "/files/88721.pdf",
                    DocumentTypeId = (int)DocumentType.Comparison,
                    ContentType = "application/pdf",
                    AttachmentFile = CreateUploadedPdf(),
                    SortOrder = 2
                });
                req.Quotations.Add(new Quotation
                {
                    Id = 88722,
                    FileName = "comp2.pdf",
                    FilePath = "/files/88722.pdf",
                    DocumentTypeId = (int)DocumentType.Comparison,
                    ContentType = "application/pdf",
                    AttachmentFile = CreateUploadedPdf(),
                    SortOrder = 3
                });
                req.Quotations.Add(new Quotation
                {
                    Id = 88723,
                    FileName = "comp3.pdf",
                    FilePath = "/files/88723.pdf",
                    DocumentTypeId = (int)DocumentType.Comparison,
                    ContentType = "application/pdf",
                    AttachmentFile = CreateUploadedPdf(),
                    SortOrder = 4
                });
                db.Requests.Add(req);
            });

            var submit2Res = await client.PostAsync($"/api/Portal/Requests/{req2Id}/submit", null);
            submit2Res.StatusCode.ShouldBe(HttpStatusCode.OK);

            _factory.SeedDatabase(db =>
            {
                var req = db.Requests.Include(r => r.Quotations).First(r => r.Id == req2Id);
                var sortedQuotations = req.Quotations.OrderBy(q => q.SortOrder).ToList();
                sortedQuotations.Select(q => q.Id).ShouldBe(new[] { 88720, 88721, 88722, 88723 });
            });

            // 3. Request whose only OriginalQuotation row is an expired reference (SourceQuotationId != null) -> fails submit
            const int req3Id = 8873;
            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Find(req3Id) != null) return;
                var req = new Request
                {
                    Id = req3Id,
                    Code = "QC-SUBMIT-ONLY-REF-ORIG",
                    Title = "Submit only reference original",
                    VendorCode = "V8873",
                    VendorName = "Vendor 8873",
                    Status = (int)RequestStatus.Draft,
                    ValidFrom = DateTime.Now,
                    ValidUntil = DateTime.Now.AddDays(30),
                    CreatedBy = "USER1",
                    IsActive = true
                };
                req.Quotations.Add(new Quotation
                {
                    FileName = "ref-as-orig.pdf",
                    FilePath = "Reference",
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    SourceQuotationId = 12345,
                    ContentType = "application/pdf",
                    AttachmentFile = CreateUploadedPdf(),
                    SortOrder = 1
                });
                db.Requests.Add(req);
            });

            var submit3Res = await client.PostAsync($"/api/Portal/Requests/{req3Id}/submit", null);
            submit3Res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public void IsPredecessorUniqueConflict_MatchesSqlServerUniqueIndexViolationMessage()
        {
            var matchingException = new DbUpdateException(
                "An error occurred while saving the entity changes.",
                new Exception("Cannot insert duplicate key row in object 'dbo.Requests' with unique index 'IX_Requests_RenewedFromRequestId'. The duplicate key value is (8501)."));

            RequestService.IsPredecessorUniqueConflict(matchingException).ShouldBeTrue();

            var unrelatedException = new DbUpdateException(
                "An error occurred while saving the entity changes.",
                new Exception("Cannot insert duplicate key row in object 'dbo.Requests' with unique index 'IX_Requests_Code'. The duplicate key value is (QC-001)."));

            RequestService.IsPredecessorUniqueConflict(unrelatedException).ShouldBeFalse();
        }

        [Fact]
        public async Task GetSourcedDocument_CoversSuccessThaiNameMismatchAndSecurity()
        {
            const int docIdPdf = 9901;
            const int docIdTxt = 9902;
            const int docIdOtherReq = 9903;
            const int docIdEmpty = 9904;

            _factory.SeedDatabase(db =>
            {
                if (db.Requests.Any(r => r.Code == "QC-DOC-01")) return;

                var pdfFile = new AttachmentFile { Id = 9901, FileSize = 100, ContentType = "application/pdf", Data = "%PDF-1.4 sample pdf content"u8.ToArray() };
                var txtFile = new AttachmentFile { Id = 9902, FileSize = 50, ContentType = "text/plain", Data = "sample text content"u8.ToArray() };
                var emptyFile = new AttachmentFile { Id = 9904, FileSize = 0, ContentType = "application/pdf", Data = Array.Empty<byte>() };

                db.AttachmentFiles.AddRange(pdfFile, txtFile, emptyFile);

                var req1 = new Request
                {
                    Id = 9901,
                    Code = "QC-DOC-01",
                    Title = "Document Test Request 1",
                    VendorCode = "V01",
                    VendorName = "Vendor 01",
                    Status = (int)RequestStatus.Completed,
                    SourceSystem = "QRS",
                    SourceCode = "QR-DOC-01",
                    Quotations = new List<Quotation>
                    {
                        new Quotation
                        {
                            Id = docIdPdf,
                            FileName = "ใบเสนอราคา.pdf",
                            FilePath = "test",
                            ContentType = "application/pdf",
                            AttachmentFileId = pdfFile.Id
                        },
                        new Quotation
                        {
                            Id = docIdTxt,
                            FileName = "notes.txt",
                            FilePath = "test",
                            ContentType = "text/plain",
                            AttachmentFileId = txtFile.Id
                        },
                        new Quotation
                        {
                            Id = docIdEmpty,
                            FileName = "empty.pdf",
                            FilePath = "test",
                            ContentType = "application/pdf",
                            AttachmentFileId = emptyFile.Id
                        }
                    }
                };

                var req2 = new Request
                {
                    Id = 9902,
                    Code = "QC-DOC-02",
                    Title = "Document Test Request 2",
                    VendorCode = "V01",
                    VendorName = "Vendor 01",
                    Status = (int)RequestStatus.Completed,
                    SourceSystem = "QRS",
                    SourceCode = "QR-DOC-02",
                    Quotations = new List<Quotation>
                    {
                        new Quotation
                        {
                            Id = docIdOtherReq,
                            FileName = "other.pdf",
                            FilePath = "test",
                            ContentType = "application/pdf",
                            AttachmentFileId = pdfFile.Id
                        }
                    }
                };

                db.Requests.AddRange(req1, req2);
            });

            var apiClient = CreateApiKeyClient();

            // 1. Success PDF with Thai name -> inline header with SetHttpFileName
            var pdfRes = await apiClient.GetAsync($"/api/Integration/Requests/QC-DOC-01/Sources/QRS/QR-DOC-01/Documents/{docIdPdf}");
            pdfRes.StatusCode.ShouldBe(HttpStatusCode.OK);
            pdfRes.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
            pdfRes.Content.Headers.ContentDisposition.ShouldNotBeNull();
            pdfRes.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
            pdfRes.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("ใบเสนอราคา.pdf");
            var pdfBytes = await pdfRes.Content.ReadAsByteArrayAsync();
            pdfBytes.ShouldBe("%PDF-1.4 sample pdf content"u8.ToArray());

            // 2. Success non-PDF -> attachment
            var txtRes = await apiClient.GetAsync($"/api/Integration/Requests/QC-DOC-01/Sources/QRS/QR-DOC-01/Documents/{docIdTxt}");
            txtRes.StatusCode.ShouldBe(HttpStatusCode.OK);
            txtRes.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");
            var txtBytes = await txtRes.Content.ReadAsByteArrayAsync();
            txtBytes.ShouldBe("sample text content"u8.ToArray());

            // 3. Mismatched QRS source code -> 404
            var mismatchQrsRes = await apiClient.GetAsync($"/api/Integration/Requests/QC-DOC-01/Sources/QRS/WRONG-QRS/Documents/{docIdPdf}");
            mismatchQrsRes.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            // 4. Mismatched QC code -> 404
            var mismatchQcRes = await apiClient.GetAsync($"/api/Integration/Requests/WRONG-QC/Sources/QRS/QR-DOC-01/Documents/{docIdPdf}");
            mismatchQcRes.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            // 5. Document from another request -> 404
            var otherDocRes = await apiClient.GetAsync($"/api/Integration/Requests/QC-DOC-01/Sources/QRS/QR-DOC-01/Documents/{docIdOtherReq}");
            otherDocRes.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            // 6. Missing / zero bytes -> 404
            var emptyBytesRes = await apiClient.GetAsync($"/api/Integration/Requests/QC-DOC-01/Sources/QRS/QR-DOC-01/Documents/{docIdEmpty}");
            emptyBytesRes.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            // 7. Anonymous -> 401
            var anonClient = _factory.CreateClient();
            var anonRes = await anonClient.GetAsync($"/api/Integration/Requests/QC-DOC-01/Sources/QRS/QR-DOC-01/Documents/{docIdPdf}");
            anonRes.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

            // 8. Wrong API key -> 401
            var wrongKeyClient = CreateApiKeyClient("invalid-api-key");
            var wrongKeyRes = await wrongKeyClient.GetAsync($"/api/Integration/Requests/QC-DOC-01/Sources/QRS/QR-DOC-01/Documents/{docIdPdf}");
            wrongKeyRes.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }
}
