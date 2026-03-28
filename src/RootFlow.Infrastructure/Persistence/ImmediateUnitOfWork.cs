using RootFlow.Application.Abstractions.Persistence;

namespace RootFlow.Infrastructure.Persistence;

public sealed class ImmediateUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }
}
