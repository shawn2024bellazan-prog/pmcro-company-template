namespace PMCR.O.Core;

public enum IdempotencyClaimStatus { Acquired, AlreadyRunning, AlreadyCompleted }
public sealed record IdempotencyClaim(IdempotencyClaimStatus Status, string ScopeKey);

/// <summary>Prevents duplicate command execution. Production deployments should use a shared durable store with atomic uniqueness.</summary>
public interface ICommandIdempotencyStore
{
    Task<IdempotencyClaim> TryBeginAsync(string actorId, string commandType, string idempotencyKey, CancellationToken cancellationToken = default);
    Task CompleteAsync(string scopeKey, CancellationToken cancellationToken = default);
    Task ReleaseAsync(string scopeKey, CancellationToken cancellationToken = default);
}

/// <summary>Thread-safe, process-local reference implementation; state is lost on restart and is not distributed.</summary>
public sealed class InMemoryCommandIdempotencyStore : ICommandIdempotencyStore
{
    private enum EntryState { Running, Completed }
    private readonly Dictionary<string, EntryState> _entries = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IdempotencyClaim> TryBeginAsync(string actorId, string commandType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(commandType) || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Actor, command type, and idempotency key are required.");
        var scope = string.Join("\u001f", actorId, commandType, idempotencyKey);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_entries.TryGetValue(scope, out var state))
                return new(state == EntryState.Completed ? IdempotencyClaimStatus.AlreadyCompleted : IdempotencyClaimStatus.AlreadyRunning, scope);
            _entries.Add(scope, EntryState.Running);
            return new(IdempotencyClaimStatus.Acquired, scope);
        }
        finally { _gate.Release(); }
    }

    public async Task CompleteAsync(string scopeKey, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { if (_entries.ContainsKey(scopeKey)) _entries[scopeKey] = EntryState.Completed; }
        finally { _gate.Release(); }
    }

    public async Task ReleaseAsync(string scopeKey, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { if (_entries.TryGetValue(scopeKey, out var state) && state == EntryState.Running) _entries.Remove(scopeKey); }
        finally { _gate.Release(); }
    }
}
