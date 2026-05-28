using EvolutionApiGateway.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace EvolutionApiGateway.Services
{
    /// <summary>
    /// Service for querying Evolution database views directly via ADO.NET.
    /// Used for read-only lookups that don't need the SDK.
    /// </summary>
    public class ViewDataService
    {
        private readonly EvolutionConfig _config;

        public ViewDataService(IOptions<EvolutionConfig> config)
        {
            _config = config.Value;
        }

        /// <summary>
        /// Builds the connection string from the Evolution config
        /// </summary>
        private string GetConnectionString(string company)
        {
            string companyDatabase = _config.GetCompanyDatabase(company);
            return $"Server={_config.Server};Database={companyDatabase};User Id={_config.Username};Password={_config.Password};Trusted_Connection=false;";
        }

        /// <summary>
        /// Returns all available expense stock items from [_uvReqExpenseStockItems]
        /// </summary>
        public List<Dictionary<string, object?>> GetExpenseStockItems(string company)
        {
            return ExecuteViewQuery(company, "SELECT * FROM [_uvReqExpenseStockItems] ORDER BY 1");
        }

        /// <summary>
        /// Returns all available trade suppliers from [_uvReqTradeSuppliers]
        /// </summary>
        public List<Dictionary<string, object?>> GetTradeSuppliers(string company)
        {
            return ExecuteViewQuery(company, "SELECT * FROM [_uvReqTradeSuppliers] ORDER BY 1");
        }

        /// <summary>
        /// Returns all created POs and their status from [_uvReqCreatedPO] view
        /// Includes: ReqNumber, PONumber, InvoiceNumber, OrderStatus
        /// Note: A PO with multiple invoices will appear as multiple rows
        /// </summary>
        public List<Dictionary<string, object?>> GetCreatedPurchaseOrders(string company)
        {
            return ExecuteViewQuery(company, "SELECT * FROM [_uvReqCreatedPO] ORDER BY PONumber DESC");
        }

        /// <summary>
        /// Returns all invoices for a specific PO from [_uvReqCreatedPO] view
        /// Filters by PO number to show all supplier invoices for that PO
        /// </summary>
        public List<Dictionary<string, object?>> GetCreatedPurchaseOrdersByPoNumber(string company, string poNumber)
        {
            string query = @"
                SELECT * 
                FROM [_uvReqCreatedPO] 
                WHERE PONumber = @PONumber
                ORDER BY InvoiceNumber
            ";

            var results = new List<Dictionary<string, object?>>();

            using (var connection = new SqlConnection(GetConnectionString(company)))
            {
                connection.Open();

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@PONumber", poNumber);

                    using (var reader = command.ExecuteReader())
                    {
                        // Get column names from the result set
                        var columns = new string[reader.FieldCount];
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columns[i] = reader.GetName(i);
                        }

                        // Read each row
                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object?>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                object? value = reader.GetValue(i);
                                // Convert DBNull to null
                                row[columns[i]] = value is DBNull ? null : value;
                            }
                            results.Add(row);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns all active projects from [_uvReqProjects]
        /// </summary>
        public List<Dictionary<string, object?>> GetProjects(string company)
        {
            return ExecuteViewQuery(company, "SELECT * FROM [_uvReqProjects] ORDER BY ProjectCode");
        }

        /// <summary>
        /// Returns outstanding supplier invoices from [_uvReqOutstandingInvoices].
        /// Use these AutoIdx values when allocating supplier payments.
        /// </summary>
        public List<Dictionary<string, object?>> GetOutstandingInvoices(string company)
        {
            return ExecuteViewQuery(company, "SELECT * FROM [_uvReqOutstandingInvoices]");
        }

        /// <summary>
        /// Generic method to execute a SELECT query against a view and return results as a list of dictionaries.
        /// Each dictionary represents a row, with column names as keys.
        /// </summary>
        private List<Dictionary<string, object?>> ExecuteViewQuery(string company, string query)
        {
            var results = new List<Dictionary<string, object?>>(); 

            using (var connection = new SqlConnection(GetConnectionString(company)))
            {
                connection.Open();

                using (var command = new SqlCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        // Get column names from the result set
                        var columns = new string[reader.FieldCount];
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columns[i] = reader.GetName(i);
                        }

                        // Read each row
                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object?>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                object? value = reader.GetValue(i);
                                // Convert DBNull to null
                                row[columns[i]] = value is DBNull ? null : value;
                            }
                            results.Add(row);
                        }
                    }
                }
            }

            return results;
        }
    }
}