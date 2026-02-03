using SDK = Pastel.Evolution;
using Local = EvolutionApiGateway.Models;
using EvolutionApiGateway.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EvolutionApiGateway.Services
{
    public class PurchaseOrderService
    {
        private readonly EvolutionConfig _config;

        public PurchaseOrderService(IOptions<EvolutionConfig> config)
        {
            _config = config.Value;
        }

        private void InitEvolution()
        {
            SDK.DatabaseContext.CreateCommonDBConnection(
                _config.Server,
                _config.CommonDatabase,
                _config.Username,
                _config.Password,
                false
            );
            SDK.DatabaseContext.SetLicense(_config.LicenseKey, _config.LicenseCode);
            SDK.DatabaseContext.CreateConnection(
                _config.Server,
                _config.CompanyDatabase,
                _config.Username,
                _config.Password,
                false
            );
        }

        /// <summary>
        /// Creates a Purchase Order in Evolution
        /// </summary>
        /// <param name="request">Purchase Order details</param>
        /// <returns>The generated PO Number</returns>
        public string CreatePurchaseOrder(Local.PurchaseOrderRequest request)
        {
            InitEvolution();

            SDK.PurchaseOrder po = new SDK.PurchaseOrder();
            po.Supplier = new SDK.Supplier(request.SupplierCode);
            po.InvoiceDate = request.InvoiceDate ?? DateTime.Now;
            
            // Set TaxMode before lines are added
            po.TaxMode = (SDK.TaxMode)Enum.Parse(typeof(SDK.TaxMode), request.TaxMode);

            // Set the External Order Number on the PO header (if provided)
            if (!string.IsNullOrWhiteSpace(request.ExternalOrderNumber))
            {
                po.ExternalOrderNo = request.ExternalOrderNumber;
            }

            // Set Purchase Requisition Number using SetUserField
            if (!string.IsNullOrWhiteSpace(request.PurchaseRequisitionNumber))
            {
                po.SetUserField("ucIDPOrdPRNr", request.PurchaseRequisitionNumber);
            }

            // Build the Description from the line item codes
            // Single line:   "Purchase Order - SECURITY"
            // Multiple lines: "Purchase Order - SECURITY, COKE, PAPER"
            string lineDescriptions = string.Join(", ", request.Lines.Select(l => l.InventoryItemCode));
            po.Description = $"Purchase Order - {lineDescriptions}";

            // Address constructor (prevents @Address5 SQL error)
            po.DeliverTo = new SDK.Address(request.DeliverToAddress ?? "", "", "", "", "", "");
            po.InvoiceTo = new SDK.Address(request.InvoiceToAddress ?? "", "", "", "", "", "");

            foreach (var line in request.Lines)
            {
                SDK.OrderDetail od = new SDK.OrderDetail();
                po.Detail.Add(od); 

                od.InventoryItem = new SDK.InventoryItem(line.InventoryItemCode);
                
                if (!string.IsNullOrEmpty(line.WarehouseCode))
                {
                    od.Warehouse = new SDK.Warehouse(line.WarehouseCode);
                }

                od.Quantity = line.Quantity;
                od.ToProcess = line.Quantity; 
                od.UnitSellingPrice = line.UnitPrice;
                od.TaxType = new SDK.TaxRate(line.TaxCode);
            }

            // Save the Purchase Order
            po.Save();
            
            return po.OrderNo;
        }

        /// <summary>
        /// Processes an existing Purchase Order into a Supplier Invoice
        /// </summary>
        public string ProcessPurchaseOrder(string poNumber, string supplierInvoiceNumber)
        {
            InitEvolution();

            if (string.IsNullOrWhiteSpace(poNumber))
                throw new ArgumentException("Purchase Order number cannot be empty", nameof(poNumber));
            if (string.IsNullOrWhiteSpace(supplierInvoiceNumber))
                throw new ArgumentException("Supplier Invoice number cannot be empty", nameof(supplierInvoiceNumber));

            SDK.PurchaseOrder po = new SDK.PurchaseOrder(poNumber);

            if (string.IsNullOrEmpty(po.OrderNo))
                throw new InvalidOperationException($"Purchase Order '{poNumber}' not found");

            po.SupplierInvoiceNo = supplierInvoiceNumber;
            string invoiceNumber = po.Process(supplierInvoiceNumber);

            if (string.IsNullOrEmpty(invoiceNumber))
                throw new InvalidOperationException("Failed to process Purchase Order - no invoice number returned");

            return invoiceNumber;
        }

        /// <summary>
        /// Gets details of an existing Purchase Order via the SDK
        /// </summary>
        public object GetPurchaseOrder(string poNumber)
        {
            InitEvolution();

            SDK.PurchaseOrder po = new SDK.PurchaseOrder(poNumber);

            if (string.IsNullOrEmpty(po.OrderNo))
                throw new InvalidOperationException($"Purchase Order '{poNumber}' not found");

            string? purchaseRequisitionNo = null;
            try { purchaseRequisitionNo = po.GetUserField("ucIDPOrdPRNr")?.ToString(); }
            catch { }

            return new
            {
                OrderNo = po.OrderNo,
                SupplierCode = po.Supplier?.Code,
                SupplierDescription = po.Supplier?.Description,
                ExternalOrderNo = po.ExternalOrderNo,
                PurchaseRequisitionNo = purchaseRequisitionNo,
                Description = po.Description,
                InvoiceDate = po.InvoiceDate,
                TaxMode = po.TaxMode.ToString(),
                Lines = GetPurchaseOrderLines(po)
            };
        }

        private List<object> GetPurchaseOrderLines(SDK.PurchaseOrder po)
        {
            var lines = new List<object>();
            foreach (SDK.OrderDetail detail in po.Detail)
            {
                lines.Add(new
                {
                    InventoryItemCode = detail.InventoryItem?.Code,
                    Description = detail.InventoryItem?.Description,
                    Quantity = detail.Quantity,
                    ToProcess = detail.ToProcess,
                    UnitPrice = detail.UnitSellingPrice,
                    TaxCode = detail.TaxType?.Code,
                    WarehouseCode = detail.Warehouse?.Code
                });
            }
            return lines;
        }
    }
}