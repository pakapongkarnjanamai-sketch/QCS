using Microsoft.Extensions.DependencyInjection;
using QCS.Application.Services;
using QCS.Domain.Enum;
using QCS.Domain.Models;
using Shouldly;
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
    }
}