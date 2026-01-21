using CustomerPlatform.Domain.Entities;

namespace Duplicatas.Domain.Interfaces
{
    public interface ISuspeitaDuplicidade
    {
        Task Add(SuspeitaDuplicidade suspeitaDuplicidade);
    }
}
