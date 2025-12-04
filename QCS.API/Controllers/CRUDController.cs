using Microsoft.AspNetCore.Mvc;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;
using System.Data;

namespace QCS.Api.Controllers
{
    public class ApprovalStepsController : GenericController<ApprovalStep>
    {
        public ApprovalStepsController(IRepository<ApprovalStep> repository, ILogger<GenericController<ApprovalStep>> logger)
           : base(repository, logger)
        {

        }
    }
    public class PurchaseRequestsController : GenericController<PurchaseRequest>
    {
        public PurchaseRequestsController(IRepository<PurchaseRequest> repository, ILogger<GenericController<PurchaseRequest>> logger)
           : base(repository, logger)
        {

        }
    }

    public class QuotationsController : GenericController<Quotation>
    {
        public QuotationsController(IRepository<Quotation> repository, ILogger<GenericController<Quotation>> logger)
           : base(repository, logger)
        {

        }
    }

}
