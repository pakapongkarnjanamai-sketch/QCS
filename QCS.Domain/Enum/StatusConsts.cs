namespace QCS.Domain.Enum
{
    public static class StatusConsts
    {
        // === PurchaseRequest Status (Int) ===
        // อ้างอิง: 0=Draft, 1=Pending, 2=Approved, 9=Rejected
        public const int PR_Draft = 0;
        public const int PR_Pending = 1;
        public const int PR_Approved = 2;
        public const int PR_Completed = 3;
        public const int PR_Rejected = 9;

        // === ApprovalStep Status (Int) ===
        // ควรกำหนดเป็น Int เช่นกันเพื่อให้ง่ายต่อการเปรียบเทียบใน Code
        public const int Step_Pending = 1;
        public const int Step_Approved = 2;
        public const int Step_Rejected = 9;
        public const int Step_Skipped = 4;
    }
}