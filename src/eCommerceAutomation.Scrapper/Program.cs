using DashFire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

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

                    services.AddEntityFrameworkSqlite().AddDbContext<Domain.AppDbContext>((sp, options) =>
                    {
                        options.UseSqlite(hostContext.Configuration.GetConnectionString("SqliteDatabase"));
                        options.UseInternalServiceProvider(sp);
                    }, ServiceLifetime.Scoped);

                    services.AddSingleton<FetcherService>();
                    services.AddSingleton<Services.FileRefService>();

                    services.AddJob<ScrapperJob>();
                })
                .UseDashFire()
                .Build()
                .Run();
        }
    }
}
