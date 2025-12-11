using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCS.Domain.Models
{
    public class WorkflowRouteDetailDto
    {
        public int Id { get; set; }
        public string RouteName { get; set; }
        public bool CanInitiate { get; set; }
        public List<WorkflowStepDto> Steps { get; set; }
    }

    public class WorkflowStepDto
    {
        public int Id { get; set; }
        public int SequenceNo { get; set; }
        public string StepName { get; set; }
        public List<WorkflowAssignmentDto> Assignments { get; set; }

        // === [NEW] เพิ่มส่วนนี้เพื่อเก็บสถานะของแต่ละ Step ===
        public int? Status { get; set; }        // 0, 1, 2, 9
        public DateTime? ActionDate { get; set; }
        public string? ApproverName { get; set; } // ชื่อคนที่กดอนุมัติจริง
        public string? ApproverNId { get; set; }
        public string? Comment { get; set; }

    }

    public class WorkflowAssignmentDto
    {
        public string NId { get; set; }
        public string EmployeeName { get; set; }
        public string AssignmentType { get; set; }

        // เพิ่มตัวนี้เข้าไปครับ
        public bool IsCurrentUser { get; set; }
    }
}
