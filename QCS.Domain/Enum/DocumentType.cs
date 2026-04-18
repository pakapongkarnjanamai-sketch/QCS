using System.ComponentModel.DataAnnotations;

namespace QCS.Domain.Enum
{
    public enum DocumentType
    {
        [Display(Name = "ORIGINAL QUOTATION")]
        OriginalQuotation = 10,

        [Display(Name = "EXPIRED QUOTATION")]
        ExpiredQuotation = 50,

        [Display(Name = "COMPARISON DOCUMENT")]
        Comparison = 20,

        [Display(Name = "PRODUCT SPECIFICATIONS")]
        Specifications = 30,

        [Display(Name = "ATTACHMENT")]
        Attachment = 40
    }
}