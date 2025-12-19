using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Services;
using QCS.Domain.DTOs;

namespace QCS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ApprovalController : ControllerBase
    {
        private readonly IRequestService _requestService;
        private readonly ILogger<ApprovalController> _logger;

        public ApprovalController(IRequestService requestService, ILogger<ApprovalController> logger)
        {
            _requestService = requestService;
            _logger = logger;
        }

        [HttpPost("Approve")]
        public async Task<IActionResult> Approve([FromBody] ApprovalActionDto input)
        {
            try
            {
                await _requestService.ApproveAsync(input);
                return Ok(new { message = "Approved successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during approval for RequestId: {RequestId}", input.RequestId);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("Reject")]
        public async Task<IActionResult> Reject([FromBody] ApprovalActionDto input)
        {
            try
            {
                await _requestService.RejectAsync(input);
                return Ok(new { message = "Rejected successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rejection for RequestId: {RequestId}", input.RequestId);
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}