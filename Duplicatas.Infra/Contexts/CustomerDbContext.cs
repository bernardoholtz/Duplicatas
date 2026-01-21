using CustomerPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CustomerPlatform.Infrastructure.Contexts;

public class CustomerDbContext : DbContext
{
    public CustomerDbContext(DbContextOptions<CustomerDbContext> options)
        : base(options)
    {
    }

    public DbSet<SuspeitaDuplicidade> Suspeitas => Set<SuspeitaDuplicidade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
