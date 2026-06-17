namespace QCS.Domain.DTOs
{
    /// <summary>
    /// Header-level view of a Quotation (Request) used by the front-end table.
    /// </summary>
    public class QuotationDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string VendorCode { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public int CurrentStepId { get; set; }
        public string RequesterName { get; set; } = string.Empty;   // the purchaser
        public string RequesterNId { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public DateTime ValidFrom { get; set; }
        public DateTime ValidUntil { get; set; }
    }
}
