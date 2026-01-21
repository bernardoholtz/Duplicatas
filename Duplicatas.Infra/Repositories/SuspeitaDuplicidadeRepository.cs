using CustomerPlatform.Domain.Entities;
using CustomerPlatform.Infrastructure.Contexts;
using Duplicatas.Domain.Interfaces;

namespace Duplicatas.Infra.Repositories
{
    public class SuspeitaDuplicidadeRepository : ISuspeitaDuplicidade
    {
        private readonly CustomerDbContext _context;

        public SuspeitaDuplicidadeRepository(CustomerDbContext context)
        {
            _context = context;
        }

        public async Task Add(SuspeitaDuplicidade suspeitaDuplicidade)
        {
            await _context.Suspeitas.AddAsync(suspeitaDuplicidade);
        }
    }
}
