namespace QCS.Domain.DTOs
{
    public class ApprovalActionDto
    {
        public int PurchaseRequestId { get; set; }
        public string Comment { get; set; }
        // public int ActionByUserId { get; set; } // ถ้าจะส่ง User ID มาจากหน้าบ้าน (ไม่แนะนำ ควรดึงจาก Token)
    }
}