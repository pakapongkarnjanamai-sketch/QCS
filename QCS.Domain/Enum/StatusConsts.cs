using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.Enum
{
    public static class StatusConsts
    {
        // สถานะของเอกสาร (PurchaseRequest)
        public const string PR_Draft = "Draft";
        public const string PR_Pending = "Pending";
        public const string PR_Approved = "Approved";
        public const string PR_Rejected = "Rejected";
        public const string PR_Completed = "Completed";

        // สถานะของขั้นตอนการอนุมัติ (ApprovalStep)
        public const string Step_Pending = "Pending";
        public const string Step_Approved = "Approved";
        public const string Step_Rejected = "Rejected";
        public const string Step_Skipped = "Skipped"; // เผื่อไว้กรณีข้ามขั้น
    }
}
