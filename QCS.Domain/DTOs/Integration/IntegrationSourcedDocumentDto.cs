namespace QCS.Domain.DTOs.Integration
{
    public sealed class IntegrationSourcedDocumentDto
    {
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public byte[] Content { get; init; } = Array.Empty<byte>();
    }
}
