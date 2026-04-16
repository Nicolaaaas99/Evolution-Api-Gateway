using Microsoft.AspNetCore.Mvc;
using EvolutionApiGateway.Services;
using System.Net;

namespace EvolutionApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ViewDataController : ControllerBase
    {
        private readonly ViewDataService _viewDataService;

        public ViewDataController(ViewDataService viewDataService)
        {
            _viewDataService = viewDataService;
        }

        /// <summary>
        /// Returns all available expense stock items
        /// </summary>
        [HttpGet("ExpenseStockItems")]
        public IActionResult GetExpenseStockItems()
        {
            try
            {
                var items = _viewDataService.GetExpenseStockItems();
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Error = "Failed to retrieve expense stock items",
                    Detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Returns all available trade suppliers
        /// </summary>
        [HttpGet("TradeSuppliers")]
        public IActionResult GetTradeSuppliers()
        {
            try
            {
                var suppliers = _viewDataService.GetTradeSuppliers();
                return Ok(suppliers);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Error = "Failed to retrieve trade suppliers",
                    Detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Returns all created Purchase Orders and their status
        /// </summary>
        [HttpGet("CreatedPurchaseOrders")]
        public IActionResult GetCreatedPurchaseOrders()
        {
            try
            {
                var purchaseOrders = _viewDataService.GetCreatedPurchaseOrders();
                return Ok(purchaseOrders);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Error = "Failed to retrieve created purchase orders",
                    Detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Returns all invoices for a specific Purchase Order
        /// </summary>
        /// <param name="poNumber">The PO number to filter by</param>
        [HttpGet("CreatedPurchaseOrders/{poNumber}")]
        public IActionResult GetCreatedPurchaseOrdersByPoNumber(string poNumber)
        {
            try
            {
                var purchaseOrders = _viewDataService.GetCreatedPurchaseOrdersByPoNumber(poNumber);
                return Ok(purchaseOrders);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Error = "Failed to retrieve purchase order invoices",
                    Detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Returns all active projects
        /// </summary>
        [HttpGet("Projects")]
        public IActionResult GetProjects()
        {
            try
            {
                var projects = _viewDataService.GetProjects();
                return Ok(projects);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new
                {
                    Error = "Failed to retrieve projects",
                    Detail = ex.Message
                });
            }
        }
    }
}