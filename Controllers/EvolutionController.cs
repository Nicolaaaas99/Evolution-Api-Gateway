// Controllers/EvolutionController.cs
using Microsoft.AspNetCore.Mvc;
using EvolutionApiGateway.Models;
using Pastel.Evolution;
// using Pastel.Evolution.SageCommon; // namespace from the SDK
using System.Net;

namespace EvolutionApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EvolutionController : ControllerBase
    {
        private void InitializeEvolutionContext()
        {
            // NOTE: Replace connection strings and credentials with secure configuration in production
            DatabaseContext.CreateCommonDBConnection("_NJ_COETZEE_\\SQLEXPRESS2019", "SageCommon", "sa", "Evolution@123", false);
            DatabaseContext.SetLicense("DE12111082", "9824607");
            DatabaseContext.CreateConnection("_NJ_COETZEE_\\SQLEXPRESS2019", "NJ Speel", "sa", "Evolution@123", false);
        }

        [HttpPost("CreateSalesOrder")]
        public IActionResult CreateSalesOrder([FromBody] SalesOrderRequest request)
        {
            try
            {
                InitializeEvolutionContext();

                // Create sales order using the SDK types
                SalesOrder SO = new SalesOrder();
                SO.Customer = new Customer(request.CustomerCode);
                SO.DeliveryDate = DateTime.Now;
                SO.Project = new Project(request.ProjectCode);
                SO.InvoiceDate = DateTime.Now;
                SO.DeliverTo = SO.Customer.PhysicalAddress.Condense();

                // Add detail line
                OrderDetail OD = new OrderDetail();
                SO.Detail.Add(OD);

                OD.InventoryItem = new InventoryItem(request.ItemCode);

                // EXPLICIT CASTS: SDK expects double
                OD.Quantity = (double)request.Quantity;
                OD.ToProcess = (double)request.Quantity;
                OD.UnitSellingPrice = (double)request.UnitSellingPrice;

                // Save and process
                SO.Save();
                string invoiceNr = SO.Process();

                return Ok(new
                {
                    Message = "Sales Order processed successfully",
                    OrderNumber = SO.OrderNo,
                    InvoiceNumber = invoiceNr
                });
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Error = "Evolution Transaction Failed",
                    Detail = ex.Message,
                    Trace = ex.StackTrace
                });
            }
        }
    }
}
