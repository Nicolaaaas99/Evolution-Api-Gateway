using SDK = Pastel.Evolution;
using Local = EvolutionApiGateway.Models;
using EvolutionApiGateway.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

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

        public (string poNumber, string? invoiceNumber) CreatePurchaseOrder(Local.PurchaseOrderRequest request)
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

            // CRITICAL: Set the Supplier Invoice Number on the PO object
            // This must be set BEFORE processing if ProcessImmediately is true
            if (request.ProcessImmediately && !string.IsNullOrWhiteSpace(request.SupplierInvoiceNumber))
            {
                po.SupplierInvoiceNo = request.SupplierInvoiceNumber;
            }

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
                
                // Set ToProcess to match the Quantity so there is data to invoice
                od.ToProcess = line.Quantity; 

                od.UnitSellingPrice = line.UnitPrice;
                od.TaxType = new SDK.TaxRate(line.TaxCode);
            }

            // Save the Purchase Order header
            po.Save();
            
            string poNumber = po.OrderNo;
            string? invoiceNumber = null;

            if (request.ProcessImmediately)
            {
                // Validate that SupplierInvoiceNumber is provided
                if (string.IsNullOrWhiteSpace(request.SupplierInvoiceNumber))
                {
                    throw new ArgumentException("SupplierInvoiceNumber is required when ProcessImmediately is true");
                }

                // The supplier invoice number should already be set on po.SupplierInvoiceNo
                // Now call Process with the same reference
                invoiceNumber = po.Process(request.SupplierInvoiceNumber);
            }

            return (poNumber, invoiceNumber);
        }
    }
}