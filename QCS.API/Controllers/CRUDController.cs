using QCS.Application.Services;
using QCS.Application.Abstractions;
using QCS.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace QCS.Api.Controllers
{
    public class CRUDApprovalStepsController : GenericController<ApprovalStep>
    {
        public CRUDApprovalStepsController(IRepository<ApprovalStep> repository, ILogger<GenericController<ApprovalStep>> logger)
           : base(repository, logger) { }
    }

    public class CRUDPurchaseRequestsController : GenericController<Request>
    {
        private readonly IRequestService _requestService;

        public CRUDPurchaseRequestsController(
            IRepository<Request> repository,
            ILogger<GenericController<Request>> logger,
            IRequestService requestService)
           : base(repository, logger)
        {
            _requestService = requestService;
        }

        [HttpDelete]
        public override async Task<IActionResult> Delete(int key)
        {
            try
            {
                await _requestService.DeleteAsync(key);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Request with id {Id}", key);
                return BadRequest(new { Message = "An error occurred while deleting the record." });
            }
        }
    }

    public class CRUDQuotationsController : GenericController<Quotation>
    {
        public CRUDQuotationsController(IRepository<Quotation> repository, ILogger<GenericController<Quotation>> logger)
           : base(repository, logger) { }
    }

}