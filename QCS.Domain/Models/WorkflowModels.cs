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
        public List<WorkflowStepDto> Steps { get; set; }
    }

    public class WorkflowStepDto
    {
        public int Id { get; set; }
        public int SequenceNo { get; set; }
        public string StepName { get; set; }
        public List<WorkflowAssignmentDto> Assignments { get; set; }
    }

    public class WorkflowAssignmentDto
    {
        public string NId { get; set; }
        public string EmployeeName { get; set; }
        public string AssignmentType { get; set; }
    }
}
