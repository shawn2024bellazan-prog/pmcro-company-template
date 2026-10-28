using System.Text.Json;
using System.Text.Json.Serialization;

namespace PMCR.O.Core;

public sealed record CapabilityRegistrySnapshot(
    string SchemaVersion,
    long Revision,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CapabilitySource> Sources,
    IReadOnlyList<CapabilityRegistration> Capabilities);

public interface ICapabilityRegistryStore
{
    Task<CapabilityRegistrySnapshot> ReadAsync(CancellationToken cancellationToken = default);
    Task<CapabilityRegistrySnapshot> UpsertSourceAsync(CapabilitySource source, CancellationToken cancellationToken = default);
    Task<CapabilityRegistrySnapshot> UpsertCapabilityAsync(CapabilityRegistration capability, CancellationToken cancellationToken = default);
}

/// <summary>Local JSON persistence for registry metadata. It stores references and metadata, never downloads or executes code.</summary>
public sealed class JsonCapabilityRegistryStore : ICapabilityRegistryStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonCapabilityRegistryStore(string path)
    {
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public async Task<CapabilityRegistrySnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await ReadUnsafeAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    public async Task<CapabilityRegistrySnapshot> UpsertSourceAsync(CapabilitySource source, CancellationToken cancellationToken = default)
    {
        var validation = CapabilityRegistryValidator.ValidateSource(source);
        if (!validation.IsValid) throw new ArgumentException("Invalid capability source: " + string.Join("; ", validation.Errors), nameof(source));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadUnsafeAsync(cancellationToken);
            var sources = current.Sources.Where(x => !string.Equals(x.SourceId, source.SourceId, StringComparison.Ordinal)).Append(source).OrderBy(x => x.SourceId, StringComparer.Ordinal).ToArray();
            return await WriteUnsafeAsync(current with { Revision = current.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow, Sources = sources }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<CapabilityRegistrySnapshot> UpsertCapabilityAsync(CapabilityRegistration capability, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadUnsafeAsync(cancellationToken);
            var validation = CapabilityRegistryValidator.ValidateCapability(capability, current.Sources.Select(x => x.SourceId).ToHashSet(StringComparer.Ordinal));
            if (!validation.IsValid) throw new ArgumentException("Invalid capability registration: " + string.Join("; ", validation.Errors), nameof(capability));
            var items = current.Capabilities.Where(x => !string.Equals(x.CapabilityId, capability.CapabilityId, StringComparison.Ordinal)).Append(capability).OrderBy(x => x.CapabilityId, StringComparer.Ordinal).ToArray();
            return await WriteUnsafeAsync(current with { Revision = current.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow, Capabilities = items }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task<CapabilityRegistrySnapshot> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return Empty();
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var snapshot = await JsonSerializer.DeserializeAsync<CapabilityRegistrySnapshot>(stream, JsonOptions, cancellationToken);
        if (snapshot is null || snapshot.SchemaVersion != "1.0" || snapshot.Revision < 0)
            throw new InvalidDataException("Capability registry snapshot is invalid or unsupported.");
        return snapshot;
    }

    private async Task<CapabilityRegistrySnapshot> WriteUnsafeAsync(CapabilityRegistrySnapshot snapshot, CancellationToken cancellationToken)
    {
        var temporary = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, _path, true);
            return snapshot;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static CapabilityRegistrySnapshot Empty() => new("1.0", 0, DateTimeOffset.UtcNow, [], []);
}
