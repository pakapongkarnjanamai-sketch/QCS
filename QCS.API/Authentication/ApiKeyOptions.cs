namespace QCS.API.Authentication
{
    public sealed class ApiKeyOptions
    {
        public const string SectionName = "Integration";

        public List<string> ApiKeys { get; set; } = [];
    }
}