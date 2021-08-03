using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace eCommerceAutomation.Scrapper
{
    public class FetcherService
    {
        private readonly ILogger<FetcherService> _logger;

        private SemaphoreSlim _urlSemaphore = new SemaphoreSlim(3, 3);

        public FetcherService(ILogger<FetcherService> logger)
        {
            _logger = logger;
        }

        public async Task<string> GetUrlContentAsync(string url)
        {
            string result;

            await _urlSemaphore.WaitAsync();
            using (var client = new HttpClient())
            {
                result = await client.GetStringAsync(url);
            }
            _urlSemaphore.Release();

            return result;
        }
    }
}
