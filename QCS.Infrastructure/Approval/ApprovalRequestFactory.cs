using Microsoft.Extensions.Options;
using QCS.Application.Abstractions;
using System.Globalization;

namespace QCS.Infrastructure.Approval
{
    public sealed class ApprovalRequestFactory : IApprovalRequestFactory
    {
        private readonly IOptionsMonitor<ApprovalServiceOptions> _options;

        public ApprovalRequestFactory(IOptionsMonitor<ApprovalServiceOptions> options)
        {
            _options = options;
        }

        public ApprovalDocumentRequest Build(ApprovalRequestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var options = _options.CurrentValue;
            var routeValue = context.RequestId?.ToString() ?? "preview";
            var sourceUrl = options.RequestUrlTemplate.Replace(
                "{id}",
                Uri.EscapeDataString(routeValue),
                StringComparison.OrdinalIgnoreCase);

            return new ApprovalDocumentRequest(
                context.Title,
                context.SourceNumber,
                sourceUrl,
                IsUrgent: false,
                context.RequesterOrgCode,
                new[] { context.RequesterOrgCode },
                new Dictionary<string, string?>
                {
                    ["vendorCode"] = context.VendorCode,
                    ["validFrom"] = context.ValidFrom?.ToString("O", CultureInfo.InvariantCulture),
                    ["validUntil"] = context.ValidUntil?.ToString("O", CultureInfo.InvariantCulture),
                    ["attachmentCount"] = context.AttachmentCount.ToString(CultureInfo.InvariantCulture),
                    ["sourceSystem"] = options.SourceSystem
                },
                context.ValidFrom);
        }
    }
}