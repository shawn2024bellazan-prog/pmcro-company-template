using System.Text.Json;
namespace PMCR.O.Core;

/// <summary>Single-process JSONL store. Not a tamper-proof or multi-process ledger.</summary>
public sealed class JsonlTrailStore : ITrailStore
{
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public JsonlTrailStore(string root) { _root = Path.GetFullPath(root); Directory.CreateDirectory(_root); }

    public async Task AppendAsync(TrailFrame frame, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (string.IsNullOrWhiteSpace(frame.CycleId) || string.IsNullOrWhiteSpace(frame.FrameType)) throw new ArgumentException("Cycle ID and frame type are required.");
        await _gate.WaitAsync(ct);
        try {
            var path = PathFor(frame.CycleId);
            var count = File.Exists(path) ? (await File.ReadAllLinesAsync(path, ct)).Length : 0;
            var ordered = frame with { Sequence = count + 1 };
            await File.AppendAllTextAsync(path, JsonSerializer.Serialize(ordered, Options) + Environment.NewLine, ct);
        } finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<TrailFrame>> ReadCycleAsync(string cycleId, CancellationToken ct)
    {
        var path = PathFor(cycleId);
        if (!File.Exists(path)) return Array.Empty<TrailFrame>();
        var frames = new List<TrailFrame>();
        foreach (var line in await File.ReadAllLinesAsync(path, ct)) {
            ct.ThrowIfCancellationRequested();
            var frame = JsonSerializer.Deserialize<TrailFrame>(line, Options);
            if (frame is not null) frames.Add(frame);
        }
        return frames;
    }

    private string PathFor(string id) {
        var safe = string.Concat(id.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        if (string.IsNullOrWhiteSpace(safe)) throw new ArgumentException("Invalid cycle ID.", nameof(id));
        return Path.Combine(_root, safe + ".jsonl");
    }
}
