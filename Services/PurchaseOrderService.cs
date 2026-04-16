using SDK = Pastel.Evolution;
using Local = EvolutionApiGateway.Models;
using EvolutionApiGateway.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;

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

        private string GetConnectionString()
        {
            return $"Server={_config.Server};Database={_config.CompanyDatabase};User Id={_config.Username};Password={_config.Password};Trusted_Connection=false;";
        }

        /// <summary>
        /// Check if a Purchase Requisition Number already exists
        /// </summary>
        private bool PurchaseRequisitionExists(string requisitionNumber)
        {
            if (string.IsNullOrWhiteSpace(requisitionNumber))
                return false;

            // Query from the User History Link table where custom fields are stored
            // UserValue contains the actual value of the custom field
            string query = @"
                SELECT COUNT(*)
                FROM _etblUserHistLink
                WHERE UserValue = @RequisitionNumber
            ";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@RequisitionNumber", requisitionNumber);
                    int count = (int)command.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// Creates a Purchase Order in Evolution
        /// </summary>
        /// <param name="request">Purchase Order details</param>
        /// <returns>The generated PO Number</returns>
        public string CreatePurchaseOrder(Local.PurchaseOrderRequest request)
        {
            // Check for duplicate Purchase Requisition Number
            if (!string.IsNullOrWhiteSpace(request.PurchaseRequisitionNumber))
            {
                if (PurchaseRequisitionExists(request.PurchaseRequisitionNumber))
                {
                    throw new InvalidOperationException(
                        $"Purchase Requisition Number '{request.PurchaseRequisitionNumber}' already exists. " +
                        "Duplicate requisition numbers are not allowed."
                    );
                }
            }

            InitEvolution();

            SDK.PurchaseOrder po = new SDK.PurchaseOrder();
            po.Supplier = new SDK.Supplier(request.SupplierCode);
            
            // Set the Order Date (when the PO was created)
            // This affects Order Date, Due Date, and GRV Date in Evolution
            po.OrderDate = request.InvoiceDate ?? DateTime.Now;
            
            // Always set TaxMode to Exclusive for Purchase Orders
            // Input Tax codes in Evolution are configured as Exclusive
            po.TaxMode = SDK.TaxMode.Exclusive;

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
            // IMPORTANT: Evolution Description field has a 40 character limit
            string lineDescriptions = string.Join(", ", request.Lines.Select(l => l.InventoryItemCode));
            string fullDescription = $"Purchase Order - {lineDescriptions}";
            
            // Truncate to 40 characters if needed
            if (fullDescription.Length > 40)
            {
                po.Description = fullDescription.Substring(0, 39);
            }
            else
            {
                po.Description = fullDescription;
            }

            // Address constructor (prevents @Address5 SQL error)
            po.DeliverTo = new SDK.Address(request.DeliverToAddress ?? "", "", "", "", "", "");
            po.InvoiceTo = new SDK.Address(request.InvoiceToAddress ?? "", "", "", "", "", "");

            foreach (var line in request.Lines)
            {
                SDK.OrderDetail od = new SDK.OrderDetail();
                po.Detail.Add(od); 

                od.InventoryItem = new SDK.InventoryItem(line.InventoryItemCode);
                
                // Set custom description if provided, otherwise Evolution uses stock item's description
                if (!string.IsNullOrWhiteSpace(line.Description))
                {
                    od.Description = line.Description;
                }
                
                if (!string.IsNullOrEmpty(line.WarehouseCode))
                {
                    od.Warehouse = new SDK.Warehouse(line.WarehouseCode);
                }

                // Set project on the line if specified
                if (!string.IsNullOrEmpty(line.ProjectCode))
                {
                    od.Project = new SDK.Project(line.ProjectCode);
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
        /// Supports full or partial processing
        /// </summary>
        public string ProcessPurchaseOrder(string poNumber, string supplierInvoiceNumber, DateTime? invoiceDate = null, List<Local.ProcessLine>? linesToProcess = null)
        {
            InitEvolution();

            if (string.IsNullOrWhiteSpace(poNumber))
                throw new ArgumentException("Purchase Order number cannot be empty", nameof(poNumber));
            if (string.IsNullOrWhiteSpace(supplierInvoiceNumber))
                throw new ArgumentException("Supplier Invoice number cannot be empty", nameof(supplierInvoiceNumber));

            SDK.PurchaseOrder po = new SDK.PurchaseOrder(poNumber);

            if (string.IsNullOrEmpty(po.OrderNo))
                throw new InvalidOperationException($"Purchase Order '{poNumber}' not found");

            // Set the invoice date (defaults to today if not specified)
            po.InvoiceDate = invoiceDate ?? DateTime.Now;

            // Get actual remaining quantities from database (accounts for previous partial processing)
            var remainingQuantities = GetRemainingQuantities(po);

            // If partial processing requested, set specific quantities on each line
            if (linesToProcess != null && linesToProcess.Count > 0)
            {
                foreach (SDK.OrderDetail detail in po.Detail)
                {
                    string itemCode = detail.InventoryItem?.Code ?? "";
                    string projectCode = detail.Project?.Code ?? "";
                    
                    // Find matching line to process
                    var lineToProcess = linesToProcess.FirstOrDefault(
                        l => l.InventoryItemCode?.Equals(itemCode, StringComparison.OrdinalIgnoreCase) == true
                    );

                    if (lineToProcess != null)
                    {
                        // Get actual remaining quantity from database using composite key
                        // Normalize to uppercase for consistent matching
                        string key = $"{itemCode.ToUpperInvariant()}|{projectCode.ToUpperInvariant()}";
                        double actualRemaining = remainingQuantities.ContainsKey(key) 
                            ? remainingQuantities[key] 
                            : 0;

                        // Validate quantity
                        if (lineToProcess.QuantityToProcess > actualRemaining)
                        {
                            throw new ArgumentException(
                                $"Cannot process {lineToProcess.QuantityToProcess} of {lineToProcess.InventoryItemCode}. " +
                                $"Only {actualRemaining} remaining to process."
                            );
                        }

                        // Set the quantity to process for this line
                        detail.ToProcess = lineToProcess.QuantityToProcess;
                    }
                    else
                    {
                        // Line not specified in partial processing - set to 0 (don't process)
                        detail.ToProcess = 0;
                    }
                }
            }
            else
            {
                // Full processing - set ToProcess to actual remaining quantities
                foreach (SDK.OrderDetail detail in po.Detail)
                {
                    string itemCode = detail.InventoryItem?.Code ?? "";
                    string projectCode = detail.Project?.Code ?? "";
                    
                    // Use composite key to handle duplicate items on different projects
                    // Normalize to uppercase for consistent matching
                    string key = $"{itemCode.ToUpperInvariant()}|{projectCode.ToUpperInvariant()}";
                    double actualRemaining = remainingQuantities.ContainsKey(key) 
                        ? remainingQuantities[key] 
                        : 0;
                    
                    detail.ToProcess = actualRemaining;
                }
            }

            po.SupplierInvoiceNo = supplierInvoiceNumber;
            string invoiceNumber = po.Process(supplierInvoiceNumber);

            if (string.IsNullOrEmpty(invoiceNumber))
                throw new InvalidOperationException("Failed to process Purchase Order - no invoice number returned");

            return invoiceNumber;
        }

        /// <summary>
        /// Calculates remaining quantities for each line from the SDK PO object
        /// The SDK automatically loads the current version with updated fQtyProcessed values
        /// However, we need to query the database because SDK's ToProcess gets reset to 0
        /// </summary>
        private Dictionary<string, double> GetRemainingQuantities(SDK.PurchaseOrder po)
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            // Query to get actual remaining quantities from PO lines
            // Use iLineProjectID to get project (not iJobID)
            string query = @"
                SELECT 
                    S.cSimpleCode AS ItemCode,
                    ISNULL(P.ProjectCode, '') AS ProjectCode,
                    L.fQuantity AS OriginalQty,
                    L.fQtyProcessed AS ProcessedQty,
                    (L.fQuantity - L.fQtyProcessed) AS RemainingQty
                FROM _btblInvoiceLines L
                INNER JOIN StkItem S ON L.iStockCodeID = S.StockLink
                INNER JOIN InvNum I ON L.iInvoiceID = I.AutoIndex
                LEFT JOIN Project P ON L.iLineProjectID = P.ProjectLink
                WHERE I.OrderNum = @PONumber
                AND I.DocFlag = 1
                AND I.DocState IN (1, 3)
                AND L.iModule = 0
                ORDER BY L.idInvoiceLines
            ";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@PONumber", po.OrderNo);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string itemCode = reader["ItemCode"].ToString() ?? "";
                            string projectCode = reader["ProjectCode"].ToString() ?? "";
                            double remainingQty = reader["RemainingQty"] != DBNull.Value 
                                ? Convert.ToDouble(reader["RemainingQty"]) 
                                : 0;
                            
                            // Use composite key: ItemCode|ProjectCode to handle duplicate items
                            // Normalize to uppercase for consistent matching
                            string key = $"{itemCode.ToUpperInvariant()}|{projectCode.ToUpperInvariant()}";
                            result[key] = remainingQty;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets details of an existing Purchase Order by querying the database directly
        /// Only returns POs that have a purchase requisition number
        /// </summary>
        public object GetPurchaseOrder(string poNumber)
        {
            // First, find the AutoIndex of the ORIGINAL PO (the one with the requisition number)
            // Match the _uvReqCreatedPO view logic exactly
            string getAutoIndexQuery = @"
                SELECT TOP 1 I.AutoIndex
                FROM InvNum I
                INNER JOIN _etblUserHistLink HL ON HL.TableID = I.AutoIndex
                WHERE I.OrderNum = @PONumber
                AND I.DocFlag = 1
                AND I.DocState IN (1, 3, 4)
                AND ISNULL(HL.UserValue, '') <> ''
                ORDER BY I.AutoIndex ASC
            ";

            int poAutoIndex;
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (var cmd = new SqlCommand(getAutoIndexQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@PONumber", poNumber);
                    var result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        throw new InvalidOperationException($"Purchase Order '{poNumber}' not found or has no purchase requisition number");
                    }
                    poAutoIndex = Convert.ToInt32(result);
                }
            }

            // Now get header and lines for this specific AutoIndex
            // Filter to get ONLY the Purchase Requisition field from _etblUserHistLink
            string headerQuery = @"
                SELECT 
                    I.OrderNum,
                    I.AccountID,
                    V.Account AS SupplierCode,
                    V.Name AS SupplierDescription,
                    I.ExtOrderNum AS ExternalOrderNo,
                    I.Description,
                    I.InvDate AS InvoiceDate,
                    HL.UserValue AS PurchaseRequisitionNo
                FROM InvNum I
                LEFT JOIN Vendor V ON I.AccountID = V.DCLink
                LEFT JOIN _etblUserHistLink HL ON HL.TableID = I.AutoIndex 
                    AND HL.UserDictID = (SELECT idUserDict FROM _rtblUserDict WHERE cFieldName = 'ucIDPOrdPRNr')
                WHERE I.AutoIndex = @AutoIndex
            ";

            string linesQuery = @"
                SELECT 
                    S.Code AS InventoryItemCode,
                    S.Description_1 AS Description,
                    L.fQuantity AS Quantity,
                    L.fUnitPriceExcl AS UnitPrice,
                    TR.Code AS TaxCode,
                    W.Code AS WarehouseCode,
                    P.ProjectCode
                FROM _btblInvoiceLines L
                INNER JOIN StkItem S ON L.iStockCodeID = S.StockLink
                LEFT JOIN TaxRate TR ON L.iTaxTypeID = TR.idTaxRate
                LEFT JOIN WhseMst W ON L.iWarehouseID = W.WhseLink
                LEFT JOIN Project P ON L.iLineProjectID = P.ProjectLink
                WHERE L.iInvoiceID = @AutoIndex
                AND L.iModule = 0
                ORDER BY L.idInvoiceLines
            ";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                connection.Open();

                // Get header
                object? header = null;
                using (var cmd = new SqlCommand(headerQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@AutoIndex", poAutoIndex);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            throw new InvalidOperationException($"Purchase Order '{poNumber}' not found");
                        }

                        header = new
                        {
                            OrderNo = reader["OrderNum"].ToString(),
                            SupplierCode = reader["SupplierCode"].ToString(),
                            SupplierDescription = reader["SupplierDescription"].ToString(),
                            ExternalOrderNo = reader["ExternalOrderNo"] != DBNull.Value ? reader["ExternalOrderNo"].ToString() : null,
                            PurchaseRequisitionNo = reader["PurchaseRequisitionNo"] != DBNull.Value ? reader["PurchaseRequisitionNo"].ToString() : null,
                            Description = reader["Description"].ToString(),
                            InvoiceDate = reader["InvoiceDate"]
                        };
                    }
                }

                // Get lines
                var lines = new List<object>();
                using (var cmd = new SqlCommand(linesQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@AutoIndex", poAutoIndex);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lines.Add(new
                            {
                                InventoryItemCode = reader["InventoryItemCode"].ToString(),
                                Description = reader["Description"].ToString(),
                                Quantity = Convert.ToDouble(reader["Quantity"]),
                                UnitPrice = Convert.ToDouble(reader["UnitPrice"]),
                                TaxCode = reader["TaxCode"].ToString(),
                                WarehouseCode = reader["WarehouseCode"] != DBNull.Value ? reader["WarehouseCode"].ToString() : null,
                                ProjectCode = reader["ProjectCode"] != DBNull.Value ? reader["ProjectCode"].ToString() : null
                            });
                        }
                    }
                }

                // Combine header and lines
                var result = new
                {
                    OrderNo = ((dynamic)header).OrderNo,
                    SupplierCode = ((dynamic)header).SupplierCode,
                    SupplierDescription = ((dynamic)header).SupplierDescription,
                    ExternalOrderNo = ((dynamic)header).ExternalOrderNo,
                    PurchaseRequisitionNo = ((dynamic)header).PurchaseRequisitionNo,
                    Description = ((dynamic)header).Description,
                    InvoiceDate = ((dynamic)header).InvoiceDate,
                    Lines = lines
                };

                return result;
            }
        }

        // Remove the old GetPurchaseOrderLines method - no longer needed
        // private List<object> GetPurchaseOrderLines(SDK.PurchaseOrder po) { ... }
    }
}