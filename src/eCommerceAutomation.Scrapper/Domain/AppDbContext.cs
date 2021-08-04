using Microsoft.EntityFrameworkCore;

namespace eCommerceAutomation.Scrapper.Domain
{
    public class AppDbContext : DbContext
    {
        private readonly DbContextOptions _options;

        public AppDbContext(DbContextOptions options) : base(options)
        {
            _options = options;
        }

        public DbSet<FileRef> FileRefs
        {
            get;
            set;
        }
    }
}
