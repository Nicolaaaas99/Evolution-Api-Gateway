using Microsoft.AspNetCore.Mvc;
using EvolutionApiGateway.Models;
using EvolutionApiGateway.Services;
using System.Net;

namespace EvolutionApiGateway.Controllers
{
    [ApiController]
    [Route("api/{company}/[controller]")]
    public class SupplierPaymentController : ControllerBase
    {
        private readonly SupplierPaymentService _paymentService;

        public SupplierPaymentController(SupplierPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// Posts a supplier payment and optionally allocates it to outstanding supplier invoices.
        /// </summary>
        /// <param name="company">Company key (e.g. "PMR")</param>
        /// <param name="request">Payment + allocation details</param>
        [HttpPost]
        public IActionResult Create(string company, [FromBody] SupplierPaymentRequest request)
        {
            try
            {
                var result = _paymentService.PostPaymentWithAllocations(company, request);

                return Ok(new
                {
                    Company = company,
                    Message = "Supplier payment posted successfully",
                    PaymentAutoIdx = result.PaymentAutoIdx,
                    PaymentReference = result.PaymentReference,
                    AllocatedInvoices = result.AllocatedInvoices
                });
            }
            catch (ArgumentException argEx)
            {
                return BadRequest(new
                {
                    Error = "Validation Error",
                    Detail = argEx.Message
                });
            }
            catch (InvalidOperationException invEx)
            {
                return NotFound(new
                {
                    Error = "Allocation Error",
                    Detail = invEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Error = "Failed to post supplier payment",
                    Detail = ex.Message,
                    Trace = ex.StackTrace
                });
            }
        }
    }
}
