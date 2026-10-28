using System.Collections.Concurrent;

namespace PMCR.O.Continuation;

/// <summary>
/// Single-process reference only. Production callers require a shared atomic durable claim store.
/// Claims are never automatically released after an uncertain dispatcher outcome.
/// </summary>
public sealed class InMemorySeedClaimStore : ISeedClaimStore
{
    private readonly ConcurrentDictionary<string, byte> _claims = new(StringComparer.Ordinal);

    public Task<bool> TryClaimAsync(string claimKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(claimKey);
        return Task.FromResult(_claims.TryAdd(claimKey, 0));
    }
}
