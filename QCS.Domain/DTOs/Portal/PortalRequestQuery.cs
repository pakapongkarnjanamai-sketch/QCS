namespace QCS.Domain.DTOs.Portal
{
    public class PortalRequestQuery
    {
        private int _pageSize = 10;
        private int _page = 1;

        public string? View { get; set; }
        public string? Search { get; set; }

        public int Page
        {
            get => _page;
            set => _page = value < 1 ? 1 : value;
        }

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1 ? 10 : (value > 100 ? 100 : value);
        }

        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }
}
