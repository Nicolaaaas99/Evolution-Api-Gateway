using System;
using System.Collections.Generic;

namespace EvolutionApiGateway.Models
{
    public class PurchaseOrderRequest
    {
        public required string SupplierCode { get; set; }
        
        // External Order Number - appears on the PO header in Evolution
        public string? ExternalOrderNumber { get; set; }
        
        // Purchase Requisition Number - appears in MFiles section on PO header
        public string? PurchaseRequisitionNumber { get; set; }
        
        public DateTime? InvoiceDate { get; set; } = DateTime.Now;
        public string? ProjectCode { get; set; }
        public string? DeliverToAddress { get; set; } 
        public string? InvoiceToAddress { get; set; } 
        
        // NOTE: TaxMode is not required in the request - it will always be set to Exclusive
        // Purchase Orders always use Exclusive tax mode because Input Tax codes are Exclusive
        
        public List<PurchaseOrderLine> Lines { get; set; } = new();
    }

    public class PurchaseOrderLine
    {
        public string? InventoryItemCode { get; set; } 
        public double Quantity { get; set; }
        public double UnitPrice { get; set; }
        
        // Tax code must be an Input Tax code (typically "15", "14", "00", etc.)
        public string TaxCode { get; set; } = "15"; 
        
        public string? WarehouseCode { get; set; }
        
        // Optional: Project code to assign to this line
        public string? ProjectCode { get; set; }
        
        // Optional: Custom description for this line
        // If provided, this overrides the stock item's description
        // If not provided, Evolution will use the stock item's default description
        public string? Description { get; set; }
    }

    // Request model for processing a PO (supports partial processing)
    public class ProcessPurchaseOrderRequest
    {
        public required string SupplierInvoiceNumber { get; set; }
        
        // Optional: Date for the supplier invoice (defaults to today if not specified)
        public DateTime? InvoiceDate { get; set; }
        
        // Optional: Specify quantities to process per line (for partial processing)
        // If not provided, all remaining quantities will be processed
        public List<ProcessLine>? LinesToProcess { get; set; }
    }

    // Specifies which lines and how much to process
    public class ProcessLine
    {
        // The inventory item code to identify which line
        public required string InventoryItemCode { get; set; }
        
        // Quantity to process for this line (must be <= ToProcess on the PO line)
        public double QuantityToProcess { get; set; }
    }
}