namespace QCS.Infrastructure.Approval
{
    public sealed class ApprovalServiceOptions
    {
        public const string SectionName = "ExternalServices:Approval";

        public string DocumentBaseUrl { get; set; } = string.Empty;
        public string WorkflowBaseUrl { get; set; } = string.Empty;
        public string SourceSystem { get; set; } = "QCS";
        public string DocumentTypeCode { get; set; } = "QC";
        public string DocumentTypeName { get; set; } = "Quotation Comparison";
        public string RequestUrlTemplate { get; set; } = string.Empty;
        public string ForwardedUserSecret { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
