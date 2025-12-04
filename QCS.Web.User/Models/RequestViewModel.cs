using System.ComponentModel.DataAnnotations;

namespace QCS.Web.User.Models
{
    public class RequestViewModel
    {
        [Required(ErrorMessage = "กรุณาระบุหัวข้อการจัดซื้อ")]
        public string Title { get; set; }

        [Required]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        // รับข้อมูลเป็น JSON String จากหน้าบ้าน แล้วค่อยแปลงกลับเป็น Object
        // (เทคนิคนี้ช่วยให้ส่ง List พร้อมไฟล์แนบได้ง่ายขึ้น)
        public string QuotationsJson { get; set; }

        // รับไฟล์แนบทั้งหมดที่ User เลือกมา
        public List<IFormFile> Attachments { get; set; }
    }
}
