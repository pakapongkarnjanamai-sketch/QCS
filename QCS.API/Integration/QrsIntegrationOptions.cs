namespace QCS.API.Integration
{
    public sealed class QrsIntegrationOptions
    {
        public const string SectionName = "ExternalServices:Qrs";

        public string BaseUrl { get; init; } = string.Empty;
        public string ApiKey { get; init; } = string.Empty;
        public int TimeoutSeconds { get; init; } = 15;
    }
}