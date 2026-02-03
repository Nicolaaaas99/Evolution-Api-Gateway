using Microsoft.AspNetCore.Mvc;
using EvolutionApiGateway.Models;
using EvolutionApiGateway.Services;
using System.Net;

namespace EvolutionApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PurchaseOrderController : ControllerBase
    {
        private readonly PurchaseOrderService _poService;

        public PurchaseOrderController(PurchaseOrderService poService)
        {
            _poService = poService;
        }

        /// <summary>
        /// Creates a new Purchase Order
        /// </summary>
        /// <param name="request">Purchase Order details</param>
        /// <returns>Created PO number</returns>
        [HttpPost]
        public IActionResult Create([FromBody] PurchaseOrderRequest request)
        {
            try
            {
                string poNumber = _poService.CreatePurchaseOrder(request);
                
                return Ok(new
                {
                    Message = "Purchase Order created successfully",
                    PONumber = poNumber
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
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Error = "Failed to create Purchase Order",
                    Detail = ex.Message,
                    Trace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Retrieves details of an existing Purchase Order
        /// </summary>
        /// <param name="poNumber">The PO number to retrieve</param>
        /// <returns>PO details</returns>
        [HttpGet("{poNumber}")]
        public IActionResult Get(string poNumber)
        {
            try
            {
                var poDetails = _poService.GetPurchaseOrder(poNumber);
                return Ok(poDetails);
            }
            catch (InvalidOperationException invEx)
            {
                return NotFound(new
                {
                    Error = "Purchase Order not found",
                    Detail = invEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Error = "Failed to retrieve Purchase Order",
                    Detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Processes an existing Purchase Order into a Supplier Invoice
        /// </summary>
        /// <param name="poNumber">The PO number to process</param>
        /// <param name="request">Supplier Invoice details</param>
        /// <returns>Generated Supplier Invoice number</returns>
        [HttpPost("{poNumber}/process")]
        public IActionResult Process(string poNumber, [FromBody] ProcessPurchaseOrderRequest request)
        {
            try
            {
                string invoiceNumber = _poService.ProcessPurchaseOrder(poNumber, request.SupplierInvoiceNumber);
                
                return Ok(new
                {
                    Message = "Purchase Order processed successfully",
                    PONumber = poNumber,
                    InvoiceNumber = invoiceNumber
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
                    Error = "Processing Error",
                    Detail = invEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Error = "Failed to process Purchase Order",
                    Detail = ex.Message,
                    Trace = ex.StackTrace
                });
            }
        }
    }
}