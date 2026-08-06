using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QCS.Application.Abstractions;
using QCS.Application.Services;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using Shouldly;
using System.Net;
using System.Text.Json;
using Xunit;

namespace QCS.Api.IntegrationTests
{
    public sealed class QuotationCentralApprovalTests : IClassFixture<QcsApiFactory>
    {
        private readonly QcsApiFactory _factory;

        public QuotationCentralApprovalTests(QcsApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GenerateStampedPdf_CentralDocumentWithoutActedAssignees_DoesNotUseLegacySteps()
        {
            _factory.ApprovalService.Reset();
            int requestId = 940;
            var documentId = Guid.NewGuid();
            const string code = "QC-20260804-940";
            _factory.ApprovalService.SeedDocument(code, documentId, "USER01", RequestStatus.Completed);
            _factory.SeedDatabase(db =>
            {
                var existing = db.Requests.Find(requestId);
                if (existing != null) db.Requests.Remove(existing);
                var request = new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = "Central stamp must be authoritative",
                    VendorCode = "V000",
                    VendorName = "Vendor",
                    Status = (int)RequestStatus.Completed,
                    ApprovalDocumentId = documentId,
                    CreatedBy = "USER01",
                    IsActive = true
                };
                request.ApprovalSteps.Add(new ApprovalStep
                {
                    Id = 9401,
                    Sequence = 1,
                    StepName = "Legacy approver",
                    Status = (int)LegacyApprovalStepStatus.Approved,
                    ApproverNId = "LEGACY01",
                    ActionDate = DateTime.Now.AddDays(-1)
                });
                db.Requests.Add(request);
            });

            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IQuotationService>();

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => service.GenerateStampedPdfAsync(requestId));
            exception.Message.ShouldContain("central Approval Service");
        }

        [Fact]
        public async Task GenerateStampedPdf_WithExpiredReference_SendsSourceBytesAsExpiredQuotation()
        {
            _factory.ApprovalService.Reset();
            const int requestId = 12200;
            const int sourceRequestId = 12201;
            const int sourceQuotationId = 1220101;
            var sourceBytes = "%PDF-1.4 referenced source"u8.ToArray();
            var documentId = Guid.NewGuid();
            const string code = "QC-20260806-12200";
            var actedAt = DateTime.Now.AddDays(-1);
            _factory.ApprovalService.SeedDocument(
                code,
                documentId,
                "USER01",
                RequestStatus.Completed,
                steps: new[]
                {
                    new ApprovalStepView(
                        1,
                        "Purchasing",
                        "Completed",
                        IsFinalStep: true,
                        Assignees: new[]
                        {
                            new ApprovalAssigneeView("APPROVER01", "Approver One", "Approved", actedAt, null)
                        })
                });
            _factory.SeedDatabase(db =>
            {
                var source = new Request
                {
                    Id = sourceRequestId,
                    Code = "QC-20250101-12201",
                    Title = "Expired source",
                    VendorCode = "V100",
                    VendorName = "Vendor 100",
                    Status = (int)RequestStatus.Completed,
                    CreatedBy = "SOURCE01",
                    IsActive = true
                };
                source.Quotations.Add(new Quotation
                {
                    Id = sourceQuotationId,
                    FileName = "old-source.pdf",
                    FilePath = "Database",
                    FileSize = sourceBytes.LongLength,
                    ContentType = "application/pdf",
                    DocumentTypeId = (int)DocumentType.OriginalQuotation,
                    SortOrder = 1,
                    AttachmentFile = new AttachmentFile
                    {
                        ContentType = "application/pdf",
                        FileSize = sourceBytes.LongLength,
                        Data = sourceBytes
                    }
                });
                var request = new Request
                {
                    Id = requestId,
                    Code = code,
                    Title = "Replacement",
                    VendorCode = "V100",
                    VendorName = "Vendor 100",
                    Status = (int)RequestStatus.Completed,
                    ApprovalDocumentId = documentId,
                    CreatedBy = "USER01",
                    IsActive = true
                };
                request.Quotations.Add(new Quotation
                {
                    FileName = "old-source.pdf",
                    FilePath = "Reference",
                    FileSize = sourceBytes.LongLength,
                    ContentType = "application/pdf",
                    DocumentTypeId = (int)DocumentType.ExpiredQuotation,
                    SortOrder = 1,
                    SourceQuotationId = sourceQuotationId
                });
                db.Requests.AddRange(source, request);
            });

            var handler = new CapturePdfHandler();
            using var scope = _factory.Services.CreateScope();
            var service = new QuotationService(
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
                scope.ServiceProvider.GetRequiredService<IDateTime>(),
                new HttpClient(handler),
                scope.ServiceProvider.GetRequiredService<IConfiguration>(),
                NullLogger<QuotationService>.Instance,
                _factory.ApprovalService);

            await service.GenerateStampedPdfAsync(requestId);

            handler.Payload.ShouldNotBeNull();
            using var payload = JsonDocument.Parse(handler.Payload);
            var pdfFile = payload.RootElement.GetProperty("pdfFiles")[0];
            pdfFile.GetProperty("documentTypeId").GetInt32().ShouldBe((int)DocumentType.ExpiredQuotation);
            Convert.FromBase64String(pdfFile.GetProperty("data").GetString()!).ShouldBe(sourceBytes);
        }

        private sealed class CapturePdfHandler : HttpMessageHandler
        {
            public string? Payload { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Payload = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent("%PDF-1.4 merged"u8.ToArray())
                };
            }
        }
    }
}