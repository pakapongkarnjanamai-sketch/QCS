using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.Models
{
    public class ApprovalStep : BaseEntity
    {
        public int Id { get; set; }
        public int PurchaseRequestId { get; set; }

        public int Sequence { get; set; } // ลำดับที่ 1, 2, 3
        public string ApproverName { get; set; } // ชื่อคนอนุมัติ (เช่น Michael Brown)
        public string Role { get; set; } // ตำแหน่ง (เช่น Manager)
        public string Status { get; set; } // Pending, Approved, Rejected
        public DateTime? ApprovalDate { get; set; }
        public string Comment { get; set; }
    }
}
