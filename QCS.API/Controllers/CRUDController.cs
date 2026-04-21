using QCS.Application.Services;
using QCS.Application.Abstractions;
using QCS.Domain.Models;

namespace QCS.Api.Controllers
{
    public class CRUDApprovalStepsController : GenericController<ApprovalStep>
    {
        public CRUDApprovalStepsController(IRepository<ApprovalStep> repository, ILogger<GenericController<ApprovalStep>> logger)
           : base(repository, logger) { }
    }

    public class CRUDPurchaseRequestsController : GenericController<Request>
    {
        public CRUDPurchaseRequestsController(IRepository<Request> repository, ILogger<GenericController<Request>> logger)
           : base(repository, logger) { }
    }

    public class CRUDQuotationsController : GenericController<Quotation>
    {
        public CRUDQuotationsController(IRepository<Quotation> repository, ILogger<GenericController<Quotation>> logger)
           : base(repository, logger) { }
    }

}