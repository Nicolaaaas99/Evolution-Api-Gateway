using System;
using System.Collections.Generic;

namespace EvolutionApiGateway.Models
{
    public class PurchaseOrderRequest
    {
        public required string SupplierCode { get; set; }
        
        // External Order Number - appears on the PO header in Evolution
        public string? ExternalOrderNumber { get; set; } 
        
        // Supplier Invoice Number - used when processing/invoicing the PO
        public string? SupplierInvoiceNumber { get; set; }
        
        public DateTime? InvoiceDate { get; set; } = DateTime.Now;
        public string? ProjectCode { get; set; }
        public string? DeliverToAddress { get; set; } 
        public string? InvoiceToAddress { get; set; } 
        public string TaxMode { get; set; } = "Exclusive";
        public List<PurchaseOrderLine> Lines { get; set; } = new();
        public bool ProcessImmediately { get; set; } = true;
    }

    public class PurchaseOrderLine
    {
        public string? InventoryItemCode { get; set; } 
        public double Quantity { get; set; }
        public double UnitPrice { get; set; }
        public string TaxCode { get; set; } = "15"; 
        public string? WarehouseCode { get; set; } 
    }
}