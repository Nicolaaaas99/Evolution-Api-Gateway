using SDK = Pastel.Evolution;
using Local = EvolutionApiGateway.Models;
using EvolutionApiGateway.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace EvolutionApiGateway.Services
{
    public class SupplierPaymentService
    {
        // Control account that supplier payments post against (instead of the default 8400)
        private const string PaymentControlAccountCode = "8490";

        private readonly EvolutionConfig _config;

        public SupplierPaymentService(IOptions<EvolutionConfig> config)
        {
            _config = config.Value;
        }

        private void InitEvolution(string company)
        {
            string companyDatabase = _config.GetCompanyDatabase(company);
            SDK.DatabaseContext.CreateCommonDBConnection(
                _config.CommonServer ?? _config.Server,
                _config.CommonDatabase,
                _config.CommonUsername ?? _config.Username,
                _config.CommonPassword ?? _config.Password,
                false
            );
            SDK.DatabaseContext.SetLicense(_config.LicenseKey, _config.LicenseCode);
            SDK.DatabaseContext.CreateConnection(
                _config.Server,
                companyDatabase,
                _config.Username,
                _config.Password,
                false
            );
        }

        private string GetConnectionString(string company)
        {
            string companyDatabase = _config.GetCompanyDatabase(company);
            return $"Server={_config.Server};Database={companyDatabase};User Id={_config.Username};Password={_config.Password};Trusted_Connection=false;";
        }

        /// <summary>
        /// Posts a supplier payment and (optionally) allocates it to one or more outstanding supplier invoices.
        /// Posting + allocation happen in a single SDK flow.
        /// </summary>
        public SupplierPaymentResult PostPaymentWithAllocations(string company, Local.SupplierPaymentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SupplierCode))
                throw new ArgumentException("SupplierCode is required", nameof(request));
            if (request.Amount <= 0)
                throw new ArgumentException("Amount must be greater than zero", nameof(request));

            InitEvolution(company);

            // 1. Post the payment
            SDK.SupplierTransaction payment = new SDK.SupplierTransaction();
            payment.Account = new SDK.Supplier(request.SupplierCode);
            payment.TransactionCode = new SDK.TransactionCode(
                SDK.Module.AP,
                string.IsNullOrWhiteSpace(request.TransactionCode) ? "PM" : request.TransactionCode
            );
            payment.Amount = request.Amount;
            payment.Reference = request.Reference ?? string.Empty;
            payment.Description = request.Description ?? "Payment";
            payment.Date = request.Date ?? DateTime.Now;

            // Post the credit side to the payment control account (8490) instead of the default 8400.
            // Note: in this SDK, OverrideDebitAccount overrides the *credit* side of the GL entry
            // (the property name and the side it affects are inverted).
            payment.OverrideDebitAccount = new SDK.GLAccount(PaymentControlAccountCode);

            payment.Post();

            // 2. Allocate to invoices (if any specified)
            var allocatedInvoices = new List<AllocatedInvoiceInfo>();
            if (request.Allocations != null && request.Allocations.Count > 0)
            {
                foreach (var alloc in request.Allocations)
                {
                    if (alloc.AutoIdx <= 0)
                        throw new ArgumentException("AutoIdx must be a positive integer", nameof(request));

                    SDK.SupplierTransaction invoice = new SDK.SupplierTransaction(alloc.AutoIdx);
                    invoice.Allocations.Add(payment);
                    invoice.Allocations.Save();

                    allocatedInvoices.Add(new AllocatedInvoiceInfo
                    {
                        InvoiceAutoIdx = alloc.AutoIdx
                    });
                }
            }

            return new SupplierPaymentResult
            {
                PaymentAutoIdx = payment.ID,
                PaymentReference = payment.Reference,
                AllocatedInvoices = allocatedInvoices
            };
        }

    }

    public class SupplierPaymentResult
    {
        public long PaymentAutoIdx { get; set; }
        public string? PaymentReference { get; set; }
        public List<AllocatedInvoiceInfo> AllocatedInvoices { get; set; } = new();
    }

    public class AllocatedInvoiceInfo
    {
        public int InvoiceAutoIdx { get; set; }
    }
}
