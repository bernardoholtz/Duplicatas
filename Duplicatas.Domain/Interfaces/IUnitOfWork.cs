using Duplicatas.Domain.Interfaces;

namespace CustomerPlatform.Domain.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        ISuspeitaDuplicidade Suspeita { get; }

        Task<int> CommitAsync();
    }
}
