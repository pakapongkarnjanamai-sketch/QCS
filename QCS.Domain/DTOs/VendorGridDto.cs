namespace QCS.Domain.DTOs
{
    public class VendorGridDto
    {
        public string VendorCode { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public string TaxId { get; set; } = "-";
        public string ContactName { get; set; } = "-";
        public string Phone { get; set; } = "-";
        public string Email { get; set; } = "-";
        public string Address { get; set; } = "-";
        public int QuotationCount { get; set; }
    }
}