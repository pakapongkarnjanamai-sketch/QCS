namespace QCS.Domain.DTOs
{
    /// <summary>
    /// Search / filter / sort / pagination parameters for the effective
    /// quotations table. Bound from the query string.
    /// </summary>
    public class EffectiveQuotationQuery
    {
        // ----- Filter / Search -----
        public string? VendorCode { get; set; }          // exact match
        public string? VendorName { get; set; }          // contains
        public string? Keyword { get; set; }             // contains across Code / Title / VendorName / VendorCode
        public DateTime? RequestDateFrom { get; set; }
        public DateTime? RequestDateTo { get; set; }
        public int? CurrentStepId { get; set; }

        // ----- Sort -----
        public string? SortBy { get; set; }              // Code, RequestDate, VendorName, ValidUntil, ...
        public bool SortDescending { get; set; } = false;

        // ----- Pagination -----
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
