namespace UnitTestsGenerator.AI.Agent.Target.Services
{
    public sealed class WebCallService
    {
        public bool Call3rdParty(string endpoint)
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(endpoint);
            try
            {
                _ = httpClient.Send(new HttpRequestMessage());
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> Call3rdPartyAsync(string endpoint)
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(endpoint);
            try
            {
                _ = await httpClient.SendAsync(new HttpRequestMessage());
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public decimal GetCommitment(decimal input)
        {
            var taxes = GetTaxes(input, "us");
            return input - taxes;
        }

        public decimal GetTaxes(decimal input, string county)
        {
            if (county == "us")
            {
                return (decimal)(input * (decimal)0.4);
            }
            return input * (decimal) 0.2;
        }
    }
}
