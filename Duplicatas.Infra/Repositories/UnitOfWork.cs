using CustomerPlatform.Domain.Interfaces;
using CustomerPlatform.Infrastructure.Contexts;
using Duplicatas.Domain.Interfaces;

namespace CustomerPlatform.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly CustomerDbContext _context;

        public ISuspeitaDuplicidade Suspeita { get; }


        public UnitOfWork(
            CustomerDbContext context,
            ISuspeitaDuplicidade suspeitaRepository)
        {
            _context = context;
            Suspeita = suspeitaRepository;
        }

        public async Task<int> CommitAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
