namespace EvolutionApiGateway.Configuration
{
    public class EvolutionConfig
    {
        public string Server { get; set; } = string.Empty;
        public string CommonDatabase { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string LicenseKey { get; set; } = string.Empty;
        public string LicenseCode { get; set; } = string.Empty;

        // Optional: separate server/credentials for the Common (registration) database
        // If not set, falls back to Server/Username/Password
        public string? CommonServer { get; set; }
        public string? CommonUsername { get; set; }
        public string? CommonPassword { get; set; }

        public Dictionary<string, CompanyConfig> Companies { get; set; } = new();

        public string GetCompanyDatabase(string companyKey)
        {
            if (Companies.TryGetValue(companyKey.ToUpperInvariant(), out var company))
                return company.CompanyDatabase;

            throw new ArgumentException($"Company '{companyKey}' not found. Available companies: {string.Join(", ", Companies.Keys)}");
        }
    }

    public class CompanyConfig
    {
        public string CompanyDatabase { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}