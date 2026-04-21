using Microsoft.AspNetCore.Mvc;
using QCS.Application.Abstractions;
using QCS.Application.Services;
using QCS.Web.Shared.Models;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkflowController : ControllerBase
    {
        private readonly WorkflowService _workflowService;
        private readonly ICurrentUserService _currentUserService;
        public WorkflowController(WorkflowService workflowService, ICurrentUserService currentUserService)
        {
            _workflowService = workflowService;
            _currentUserService = currentUserService;
        }

        [HttpGet("route/{id}")]
        public async Task<IActionResult> GetRouteDetail(int id)
        {
            var nId = _currentUserService.UserId;
            // รองรับการส่ง createdBy ผ่าน Query String (เช่น ?createdBy=n4734)
            var result = await _workflowService.GetWorkflowRouteDetailAsync(id, nId);

            if (result == null)
            {
                return NotFound("Could not fetch workflow data.");
            }
            return Ok(result);
        }
    }
}