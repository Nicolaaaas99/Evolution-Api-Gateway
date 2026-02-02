// Models/SalesOrderRequest.cs
namespace EvolutionApiGateway.Models
{
    public class SalesOrderRequest
    {
        public required string CustomerCode { get; set; }
        public required string ProjectCode { get; set; }
        public required string ItemCode { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitSellingPrice { get; set; }
    }
}
