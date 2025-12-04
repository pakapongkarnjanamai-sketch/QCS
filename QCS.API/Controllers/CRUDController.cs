using Microsoft.AspNetCore.Mvc;
using QCS.Domain.Models;
using QCS.Infrastructure.Services;
using System.Data;

namespace QCS.Api.Controllers
{
    public class ApprovalStepController : GenericController<ApprovalStep>
    {
        public ApprovalStepController(IRepository<ApprovalStep> repository, ILogger<GenericController<ApprovalStep>> logger)
           : base(repository, logger)
        {

        }
    }
    public class PurchaseRequestController : GenericController<PurchaseRequest>
    {
        public PurchaseRequestController(IRepository<PurchaseRequest> repository, ILogger<GenericController<PurchaseRequest>> logger)
           : base(repository, logger)
        {

        }
    }

    public class QuotationController : GenericController<Quotation>
    {
        public QuotationController(IRepository<Quotation> repository, ILogger<GenericController<Quotation>> logger)
           : base(repository, logger)
        {

        }
    }

}
