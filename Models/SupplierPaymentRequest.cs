using System;
using System.Collections.Generic;

namespace EvolutionApiGateway.Models
{
    public class SupplierPaymentRequest
    {
        public required string SupplierCode { get; set; }

        // Total payment amount
        public double Amount { get; set; }

        // Reference for the payment (e.g. "EFT123", cheque number)
        public string? Reference { get; set; }

        // Description for the payment line in Evolution
        public string? Description { get; set; }

        // Optional: date of the payment (defaults to today)
        public DateTime? Date { get; set; }

        // Optional: transaction code (defaults to "PM" - Payment Made)
        public string? TransactionCode { get; set; }

        // Optional: list of supplier invoices to allocate this payment against.
        // If omitted, the payment is posted as an unallocated credit.
        public List<SupplierPaymentAllocation>? Allocations { get; set; }
    }

    public class SupplierPaymentAllocation
    {
        // The PostAP AutoIdx of the supplier invoice to allocate against
        public int AutoIdx { get; set; }
    }
}
