using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.DTOs
{
    public class PermissionDto
    {
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public bool CanEdit { get; set; }
    }
}
