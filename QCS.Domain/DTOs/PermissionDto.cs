using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.DTOs
{
    public class PermissionDto
    {
        public bool CanSubmit { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public bool CanReturn { get; set; }
        public bool CanCancel { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool IsCreator { get; set; }
        public bool IsCurrentAssignee { get; set; }
        public List<string> AvailableActions { get; set; } = new();
    }
}
