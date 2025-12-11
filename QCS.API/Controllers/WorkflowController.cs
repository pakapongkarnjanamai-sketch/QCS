using Microsoft.AspNetCore.Mvc;
using QCS.Application.Services;
using QCS.Web.Shared.Models;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkflowController : ControllerBase
    {
        private readonly WorkflowService _workflowService;

        public WorkflowController(WorkflowService workflowService)
        {
            _workflowService = workflowService;
        }

        [HttpGet("route/{id}")]
        public async Task<IActionResult> GetRouteDetail(int id)
        {
            var result = await _workflowService.GetWorkflowRouteDetailAsync(id);
            if (result == null)
            {
                return NotFound("Could not fetch workflow data.");
            }
            return Ok(result);
        }
    }
}