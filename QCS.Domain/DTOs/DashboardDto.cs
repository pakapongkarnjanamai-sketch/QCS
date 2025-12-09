using QCS.Domain.Models;
using System.Collections.Generic;

namespace QCS.Domain.DTOs
{
    public class DashboardDto
    {
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public List<PurchaseRequest> RecentRequests { get; set; }
    }
}