using System.ComponentModel.DataAnnotations;

namespace QCS.Domain.Enum
{
    public enum DocumentType
    {
        [Display(Name = "ใบเสนอราคาหลัก")]
        MainQuotation = 10,

        [Display(Name = "ใบเสนอราคาเปรียบเทียบ")]
        ComparativeQuotation = 20,

        [Display(Name = "รายละเอียดคุณลักษณะ (Spec)")]
        Specifications = 30,

        [Display(Name = "เงื่อนไขและข้อกำหนด")]
        TermsAndConditions = 40
    }
}