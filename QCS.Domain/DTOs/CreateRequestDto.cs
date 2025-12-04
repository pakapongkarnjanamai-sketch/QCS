using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.DTOs
{
    // DTOs
    public class CreateRequestDto
    {
        public string Title { get; set; }
        public DateTime RequestDate { get; set; }
        public string QuotationsJson { get; set; }
        public List<IFormFile> Attachments { get; set; }
    }
}
