namespace QCS.Domain.DTOs
{
    public class RequesterGridDto
    {
        public string RequesterNId { get; set; } = string.Empty;
        public string RequesterName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = "-";
        public int QuotationCount { get; set; }
    }
}