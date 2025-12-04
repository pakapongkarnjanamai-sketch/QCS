using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.DTOs
{
    public class QuotationItemDto
    {
        public string VendorName { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsSelected { get; set; }
        public string FileName { get; set; }
    }
}
