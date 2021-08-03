using DashFire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace eCommerceAutomation.Scrapper
{
    class Program
    {
        static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .UseSystemd()
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<ApplicationOptions>(options => hostContext.Configuration.GetSection("ApplicationOptions").Bind(options));

                    services.AddSingleton<FetcherService>();

                    services.AddJob<ScrapperJob>();
                })
                .UseDashFire()
                .Build()
                .Run();
        }
    }
}
