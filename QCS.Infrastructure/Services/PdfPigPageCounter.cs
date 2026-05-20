using Microsoft.Extensions.Logging;
using QCS.Application.Abstractions;
using UglyToad.PdfPig;

namespace QCS.Infrastructure.Services
{
    public class PdfPigPageCounter : IPdfPageCounter
    {
        private readonly ILogger<PdfPigPageCounter> _logger;

        public PdfPigPageCounter(ILogger<PdfPigPageCounter> logger)
        {
            _logger = logger;
        }

        public int? CountPages(byte[]? data, string? contentType)
        {
            if (data == null || data.Length == 0) return null;

            if (!string.IsNullOrWhiteSpace(contentType)
                && !contentType.Contains("pdf", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                using var doc = PdfDocument.Open(data);
                return doc.NumberOfPages;
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read PDF for page count (size={Size}).", data.Length);
                return null;
            }
        }
    }
}
