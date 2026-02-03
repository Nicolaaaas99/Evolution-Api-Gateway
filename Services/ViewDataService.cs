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
        private string GetConnectionString()
        {
            return $"Server={_config.Server};Database={_config.CompanyDatabase};User Id={_config.Username};Password={_config.Password};Trusted_Connection=false;";
        }

        /// <summary>
        /// Returns all available expense stock items from [_uvReqExpenseStockItems]
        /// </summary>
        public List<Dictionary<string, object?>> GetExpenseStockItems()
        {
            return ExecuteViewQuery("SELECT * FROM [_uvReqExpenseStockItems] ORDER BY 1");
        }

        /// <summary>
        /// Returns all available trade suppliers from [_uvReqTradeSuppliers]
        /// </summary>
        public List<Dictionary<string, object?>> GetTradeSuppliers()
        {
            return ExecuteViewQuery("SELECT * FROM [_uvReqTradeSuppliers] ORDER BY 1");
        }

        /// <summary>
        /// Returns all created POs and their status from [_uvReqCreatedPO]
        /// </summary>
        public List<Dictionary<string, object?>> GetCreatedPurchaseOrders()
        {
            return ExecuteViewQuery("SELECT * FROM [_uvReqCreatedPO] ORDER BY 1");
        }

        /// <summary>
        /// Generic method to execute a SELECT query against a view and return results as a list of dictionaries.
        /// Each dictionary represents a row, with column names as keys.
        /// </summary>
        private List<Dictionary<string, object?>> ExecuteViewQuery(string query)
        {
            var results = new List<Dictionary<string, object?>>();

            using (var connection = new SqlConnection(GetConnectionString()))
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