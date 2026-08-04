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
    }
}
