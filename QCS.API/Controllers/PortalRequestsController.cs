using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QCS.Application.Services;
using QCS.Domain.DTOs.Portal;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QCS.API.Controllers
{
    [Route("api/Portal/Requests")]
    [ApiController]
    [Authorize(Policy = "DomainUser")]
    public class PortalRequestsController : ControllerBase
    {
        private readonly IRequestService _requestService;

        public PortalRequestsController(IRequestService requestService)
        {
            _requestService = requestService;
        }

        [HttpGet]
        public async Task<ActionResult<PortalPage<PortalRequestListItemDto>>> GetRequests(
            [FromQuery] PortalRequestQuery query,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query.View) ||
                !Enum.TryParse<PortalRequestView>(query.View, ignoreCase: true, out _))
            {
                return Problem(
                    detail: $"Invalid view '{query.View}'. Valid values are: {nameof(PortalRequestView.MyTasks)}, {nameof(PortalRequestView.MyRequests)}, {nameof(PortalRequestView.MyApproved)}, {nameof(PortalRequestView.Rejected)}, {nameof(PortalRequestView.AllApproved)}.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request");
            }

            var result = await _requestService.GetPortalRequestsAsync(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(PortalRequestDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRequestById([FromRoute] int id, CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request",
                    detail: "Route parameter 'id' must be greater than 0.");
            }

            try
            {
                var result = await _requestService.GetPortalRequestByIdAsync(id, cancellationToken);
                if (result == null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Document not found",
                        detail: "Request document not found.");
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: "You do not have permission to view this request.");
            }
        }

        [HttpGet("by-code/{code}")]
        [ProducesResponseType(typeof(PortalRequestDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRequestByCode([FromRoute] string code, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid request",
                    detail: "Route parameter 'code' is required.");
            }

            try
            {
                var result = await _requestService.GetPortalRequestByCodeAsync(code, cancellationToken);
                if (result == null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Document not found",
                        detail: "Request document not found.");
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: "You do not have permission to view this request.");
            }
        }
    }
}
