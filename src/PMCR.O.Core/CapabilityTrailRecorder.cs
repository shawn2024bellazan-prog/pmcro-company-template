using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PMCR.O.Core;

public interface IEnterpriseTrailStore
{
    Task AppendAsync(EnterpriseTrailFrame frame, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EnterpriseTrailFrame>> ReadRunAsync(string runId, CancellationToken cancellationToken = default);
}

/// <summary>Single-process JSONL persistence for enterprise frames. Hashes provide tamper evidence, not immutability.</summary>
public sealed class JsonlEnterpriseTrailStore : IEnterpriseTrailStore
{
    private readonly string _root;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JsonlEnterpriseTrailStore(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public async Task AppendAsync(EnterpriseTrailFrame frame, CancellationToken cancellationToken = default)
    {
        var validation = EnterpriseTrailFrameValidator.Validate(frame);
        if (!validation.IsValid) throw new ArgumentException("Invalid enterprise Trail Frame: " + string.Join("; ", validation.Errors), nameof(frame));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var path = PathFor(frame.RunId);
            string[] existing = File.Exists(path) ? await File.ReadAllLinesAsync(path, cancellationToken) : Array.Empty<string>();
            var prior = existing.Length == 0 ? null : JsonSerializer.Deserialize<StoredEnterpriseTrailFrame>(existing[^1], JsonOptions);
            var sequenced = frame with { Sequence = existing.Length + 1, ParentFrameId = prior?.Frame.FrameId, CausalFrameIds = prior is null ? frame.CausalFrameIds : frame.CausalFrameIds.Append(prior.Frame.FrameId).Distinct(StringComparer.Ordinal).ToArray() };
            var canonical = JsonSerializer.Serialize(sequenced, JsonOptions);
            var priorHash = prior?.FrameHash ?? "GENESIS";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(priorHash + "\n" + canonical))).ToLowerInvariant();
            var stored = new StoredEnterpriseTrailFrame(sequenced, priorHash, hash);
            await File.AppendAllTextAsync(path, JsonSerializer.Serialize(stored, JsonOptions) + Environment.NewLine, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<EnterpriseTrailFrame>> ReadRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(runId);
        if (!File.Exists(path)) return Array.Empty<EnterpriseTrailFrame>();
        var result = new List<EnterpriseTrailFrame>();
        foreach (var line in await File.ReadAllLinesAsync(path, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stored = JsonSerializer.Deserialize<StoredEnterpriseTrailFrame>(line, JsonOptions);
            if (stored is not null) result.Add(stored.Frame);
        }
        return result;
    }

    /// <summary>Verifies stored sequence and hash linkage. Does not protect against an attacker rewriting the whole file.</summary>
    public async Task<bool> VerifyIntegrityAsync(string runId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(runId);
        if (!File.Exists(path)) return true;
        var priorHash = "GENESIS";
        var expectedSequence = 1;
        foreach (var line in await File.ReadAllLinesAsync(path, cancellationToken))
        {
            var stored = JsonSerializer.Deserialize<StoredEnterpriseTrailFrame>(line, JsonOptions);
            if (stored is null || stored.PreviousHash != priorHash || stored.Frame.Sequence != expectedSequence++) return false;
            var canonical = JsonSerializer.Serialize(stored.Frame, JsonOptions);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(priorHash + "\n" + canonical))).ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(stored.FrameHash))) return false;
            priorHash = stored.FrameHash;
        }
        return true;
    }

    private string PathFor(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Run ID is required.", nameof(id));
        var safe = string.Concat(id.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        return Path.Combine(_root, safe + ".enterprise.jsonl");
    }

    private sealed record StoredEnterpriseTrailFrame(EnterpriseTrailFrame Frame, string PreviousHash, string FrameHash);
}

/// <summary>Writes lifecycle evidence around capability discovery/validation/policy/invocation.</summary>
public interface ICapabilityTrailRecorder
{
    Task RecordAsync(CapabilityRegistration capability, CapabilityInvocationRequest request, string phase, string status,
        string authorizationDecision, IReadOnlyList<string> evidence, string? approvalRef = null, CancellationToken cancellationToken = default);
}

public sealed class EnterpriseCapabilityTrailRecorder(IEnterpriseTrailStore store, string organizationId, string projectId, string agentId = "pmcro.capability-executor") : ICapabilityTrailRecorder
{
    public async Task RecordAsync(CapabilityRegistration capability, CapabilityInvocationRequest request, string phase, string status,
        string authorizationDecision, IReadOnlyList<string> evidence, string? approvalRef = null, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var runId = request.RunId ?? ("caprun-" + request.ObjectiveId);
        var safeRunId = string.Concat(runId.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        var frame = new EnterpriseTrailFrame(
            "1.0", organizationId, projectId, request.IntentId ?? request.ObjectiveId, safeRunId, agentId, safeRunId,
            "frame-" + Guid.NewGuid().ToString("N"), 1, null, [], now,
            request.OriginalSeed ?? request.ObjectiveId, request.RefinedObjective ?? request.ObjectiveId, [], null, phase, capability.CapabilityId,
            "capability.invoke", status, evidence, capability.PolicyRef, authorizationDecision, approvalRef,
            status == "succeeded" ? "PASS" : "NOT_PASS", string.Join("; ", evidence), [], false,
            null, "observation", null, capability.Provenance, false, capability.ContentDigest);
        await store.AppendAsync(frame, cancellationToken);
    }
}
