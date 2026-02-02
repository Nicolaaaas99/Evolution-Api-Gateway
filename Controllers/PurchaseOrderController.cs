using Microsoft.AspNetCore.Mvc;
using EvolutionApiGateway.Models;
using EvolutionApiGateway.Services;
using Pastel.Evolution;
using System.Net;
using System.Text.Json;

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

        [HttpPost]
        public IActionResult Create([FromBody] PurchaseOrderRequest request)
        {
            try
            {
                // Debug: Log the incoming request
                var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine($"DEBUG - Received Request:\n{requestJson}");
                Console.WriteLine($"DEBUG - SupplierInvoiceNumber value: '{request.SupplierInvoiceNumber ?? "NULL"}'");
                Console.WriteLine($"DEBUG - SupplierInvoiceNumber length: {request.SupplierInvoiceNumber?.Length ?? 0}");
                
                var result = _poService.CreatePurchaseOrder(request);
                return Ok(new
                {
                    Message = "Purchase Order created successfully",
                    PONumber = result.poNumber,
                    InvoiceNumber = result.invoiceNumber
                });
            }
            catch (ArgumentException argEx)
            {
                // This will catch our custom validation error
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
    }
}