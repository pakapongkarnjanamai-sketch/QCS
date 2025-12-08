using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.DTOs
{
    public class ApprovalActionDto
    {
        public int StepId { get; set; }
        public string Comment { get; set; }
        public bool IsApproved { get; set; }
    }
}
