using System.Collections.Generic;

namespace QCS.Domain.DTOs.Portal
{
    public class PortalPage<T>
    {
        public IReadOnlyList<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasNextPage => (long)Page * PageSize < TotalCount;

        public PortalPage() { }

        public PortalPage(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
        {
            Items = items ?? new List<T>();
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
        }
    }
}
