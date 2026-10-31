namespace CSP_API.Services.CSP
{
    public interface ICspService
    {
        public Task<Dictionary<string, object>> DemoCode(Dictionary<string, object> request);

        public Task<Dictionary<string, object>> QueryVendor(Dictionary<string, object> request);

        public Task<Dictionary<string, object>> UpsertVendor(Dictionary<string, object> request);

        public Task<Dictionary<string, object>> QueryOptions(Dictionary<string, object> request);

        public Task<Dictionary<string, object>> CreateInvoice(Dictionary<string, object> request);

        public Task<Dictionary<string, object>> QueryInvoice(Dictionary<string, object> request);

        public Task<Dictionary<string, object>> UpdateInvoiceStatus(Dictionary<string, object> request);

        public Task<Dictionary<string, object>> UpsertPayment(Dictionary<string, object> request);
        
        public Task<Dictionary<string, object>> ExportInvoice(Dictionary<string, object> request);
    }
}