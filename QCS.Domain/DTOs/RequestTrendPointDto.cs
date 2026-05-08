namespace QCS.Domain.DTOs
{
    public class RequestTrendPointDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
