using System.Text.Json;

namespace QCS.Application.Abstractions
{
    public class QrsSourcingException : Exception
    {
        public int? StatusCode { get; }
        public bool IsContractViolation { get; }

        public QrsSourcingException(
            string message,
            int? statusCode = null,
            Exception? innerException = null,
            bool isContractViolation = false)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            IsContractViolation = isContractViolation;
        }
    }

    public enum QrsRequestType
    {
        Goods = 0,
        Service = 1,
        Food = 2,
        ContractRenewal = 3,
        Medicine = 4,
        Other = 5
    }

    public enum QrsRequestIntent
    {
        New = 0,
        Renewal = 1
    }

    public sealed class QrsSourcingListDto
    {
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public int RequestType { get; init; }
        public string RequestTypeName { get; init; } = string.Empty;
        public string RequesterNId { get; init; } = string.Empty;
        public string RequesterName { get; init; } = string.Empty;
        public string? RequesterDepartment { get; init; }
        public string Currency { get; init; } = string.Empty;
        public decimal EstimatedTotal { get; init; }
        public bool IsUrgent { get; init; }
        public DateTime? RequiredBy { get; init; }
        public DateTime? SubmittedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public int ItemCount { get; init; }
        public int AttachmentCount { get; init; }
        public int Intent { get; init; }
        public string IntentName { get; init; } = string.Empty;
    }

    public sealed class QrsSourcingPagedResultDto
    {
        public IReadOnlyList<QrsSourcingListDto> Items { get; init; } = Array.Empty<QrsSourcingListDto>();
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public int TotalPages { get; init; }
        public int TotalCount { get; init; }
        public bool HasPreviousPage { get; init; }
        public bool HasNextPage { get; init; }
    }

    public sealed class QrsSourcingDetailDto
    {
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public int RequestType { get; init; }
        public string RequestTypeName { get; init; } = string.Empty;
        public string RequesterNId { get; init; } = string.Empty;
        public string RequesterName { get; init; } = string.Empty;
        public string? RequesterDepartment { get; init; }
        public string Currency { get; init; } = string.Empty;
        public decimal EstimatedTotal { get; init; }
        public bool IsUrgent { get; init; }
        public DateTime? RequiredBy { get; init; }
        public DateTime? SubmittedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public int ItemCount { get; init; }
        public int AttachmentCount { get; init; }
        public string? Purpose { get; init; }
        public JsonElement? TypeDetails { get; init; }
        public JsonElement? Items { get; init; }
        public JsonElement? Attachments { get; init; }
        public int Intent { get; init; }
        public string IntentName { get; init; } = string.Empty;
        public string? PreviousQcCode { get; init; }
        public string? RenewalReason { get; init; }
    }

    public interface IQrsSourcingService
    {
        Task<QrsSourcingPagedResultDto> GetRequestsAsync(string? search, int page, int pageSize, string? intent, CancellationToken cancellationToken = default);
        Task<QrsSourcingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    }
}
