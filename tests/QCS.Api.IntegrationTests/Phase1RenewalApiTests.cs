using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Abstractions;
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
    }
}
