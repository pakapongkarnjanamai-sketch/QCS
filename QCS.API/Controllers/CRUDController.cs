using Microsoft.AspNetCore.Mvc;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;

namespace QCS.Api.Controllers
{
    public class CRUDApprovalStepsController : GenericController<ApprovalStep>
    {
        public CRUDApprovalStepsController(IRepository<ApprovalStep> repository, ILogger<GenericController<ApprovalStep>> logger)
           : base(repository, logger) { }
    }

    public class CRUDPurchaseRequestsController : GenericController<PurchaseRequest>
    {
        public CRUDPurchaseRequestsController(IRepository<PurchaseRequest> repository, ILogger<GenericController<PurchaseRequest>> logger)
           : base(repository, logger) { }
    }

    public class CRUDQuotationsController : GenericController<Quotation>
    {
        public CRUDQuotationsController(IRepository<Quotation> repository, ILogger<GenericController<Quotation>> logger)
           : base(repository, logger) { }
    }

}