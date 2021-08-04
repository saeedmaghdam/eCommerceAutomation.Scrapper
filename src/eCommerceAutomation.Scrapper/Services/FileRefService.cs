using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace eCommerceAutomation.Scrapper.Services
{
    public class FileRefService
    {
        private readonly Domain.AppDbContext _db;

        public FileRefService(Domain.AppDbContext db)
        {
            _db = db;
        }

        public async Task<string> CreateAsync(string url, CancellationToken cancellationToken)
        {
            var fileName = $"{Guid.NewGuid()}.txt";

            _db.FileRefs.Add(new Domain.FileRef()
            {
                FileName = fileName,
                Url = url
            });

            await _db.SaveChangesAsync(cancellationToken);

            return fileName;
        }

        public async Task<Domain.FileRef> GetByUrlAsync(string url, CancellationToken cancellationToken)
        {
            var item = await _db.FileRefs.Where(x => x.Url == url).SingleOrDefaultAsync(cancellationToken);
            if (item == null)
                return default(Domain.FileRef);

            return item;
        }
    }
}
