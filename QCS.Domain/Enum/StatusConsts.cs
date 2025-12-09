namespace QCS.Domain.Enum
{
    public static class StatusConsts
    {
        // === PurchaseRequest Status (สถานะของเอกสารทั้งใบ) ===
        public const int PR_Draft = 0;      // แบบร่าง
        public const int PR_Pending = 1;    // รออนุมัติ (อยู่ในกระบวนการ)
        public const int PR_Approved = 2;   // อนุมัติครบถ้วนแล้ว
        public const int PR_Completed = 3;  // จบกระบวนการ (อาจจะหมายถึงเปิด PO แล้ว หรือได้รับของแล้ว)
        public const int PR_Rejected = 9;   // ถูกไม่อนุมัติ (จบกระบวนการ)
        public const int PR_Cancelled = 99; // ยกเลิกโดยผู้ขอซื้อ

        // === ApprovalStep Status (สถานะของแต่ละขั้นตอนย่อย) ===
        public const int Step_Draft = 0;    // ยังมาไม่ถึงขั้นตอนนี้
        public const int Step_Pending = 1;  // ถึงตาเราแล้ว (รอพิจารณา)
        public const int Step_Approved = 2; // อนุมัติผ่านแล้ว
        public const int Step_Rejected = 9; // ถูกตีกลับ/ไม่อนุมัติที่ขั้นตอนนี้
        public const int Step_Skipped = 4;  // ข้าม (กรณีเงื่อนไขไม่ถึง เช่น ยอดเงินน้อยไม่ต้องผ่าน CEO)
    }
}