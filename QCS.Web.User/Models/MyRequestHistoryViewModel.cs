using System.ComponentModel.DataAnnotations;

namespace QCS.Web.User.Models
{
    public class MyRequestHistoryViewModel
    {
        public int Id { get; set; }
        public string DocumentNo { get; set; }
        public string Title { get; set; }

        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime RequestDate { get; set; }

        public string Status { get; set; } // เช่น Draft, Pending Manager, Approved
        public decimal TotalAmount { get; set; } // ยอดเงินรวม (ของเจ้าที่เลือก)

        // ข้อมูลเพิ่มเติมสำหรับแสดงผล
        public string CurrentHandler { get; set; } // ใครกำลังถือเอกสารอยู่
        public bool IsCompleted { get; set; }
    }
}