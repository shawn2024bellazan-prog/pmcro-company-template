using System.Security.Cryptography;
using System.Text;

namespace PMCR.O.SubagentReturn;

/// <summary>
/// Reference host-side proof authority. An authenticated host must bind the external
/// checker principal before issuing a proof. This does not authenticate a principal
/// merely because a caller supplies its string value. Keys must be protected by the
/// host and rotated independently of the repository.
/// </summary>
public sealed class HmacCheckerProofAuthority : IAuthenticatedCheckerEvidenceVerifier
{
    private readonly byte[] _key;
    private readonly IReadOnlySet<string> _authorizedCheckers;

    public HmacCheckerProofAuthority(byte[] trustedKey, IReadOnlySet<string> authorizedCheckers)
    {
        ArgumentNullException.ThrowIfNull(trustedKey);
        ArgumentNullException.ThrowIfNull(authorizedCheckers);
        if (trustedKey.Length < 32) throw new ArgumentException("A minimum 256-bit host key is required.", nameof(trustedKey));
        _key = (byte[])trustedKey.Clone();
        _authorizedCheckers = authorizedCheckers;
    }

    public CheckerAttestation IssueForAuthenticatedChecker(
        string authenticatedCheckerId, string frameId, string evidenceDigest,
        string resultDigest, string runId, string correlationId)
    {
        if (!_authorizedCheckers.Contains(authenticatedCheckerId))
            throw new UnauthorizedAccessException("Checker principal is not authorized.");
        var payload = Payload(authenticatedCheckerId, frameId, evidenceDigest, resultDigest, runId, correlationId);
        var signature = Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return new CheckerAttestation(true, authenticatedCheckerId, frameId, evidenceDigest, resultDigest, "hmac-sha256:" + signature);
    }

    public Task<bool> VerifyAsync(CheckerAttestation a, SubagentReturnEnvelope returned,
        TrustedSubagentInvocation invocation, PMCR.O.Core.EnterpriseTrailFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!a.Verified || !_authorizedCheckers.Contains(a.CheckerAgentId) ||
            a.CheckerAgentId == invocation.Caller.ExpectedChildAgent ||
            a.EvidenceFrameId != frame.FrameId || a.EvidenceDigest != frame.PayloadDigest ||
            a.ResultDigest != SubagentResultDigest.Compute(returned.FinalResult!) ||
            frame.CheckerVerdict != "PASS" || frame.AgentId != a.CheckerAgentId ||
            a.Signature is null || !a.Signature.StartsWith("hmac-sha256:", StringComparison.Ordinal))
            return Task.FromResult(false);
        var payload = Payload(a.CheckerAgentId, frame.FrameId, a.EvidenceDigest!, a.ResultDigest!,
            invocation.Caller.RunId, invocation.Caller.CorrelationId);
        var expected = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
        byte[] supplied;
        try { supplied = Convert.FromHexString(a.Signature["hmac-sha256:".Length..]); }
        catch (FormatException) { return Task.FromResult(false); }
        return Task.FromResult(supplied.Length == expected.Length &&
            CryptographicOperations.FixedTimeEquals(supplied, expected));
    }

    private static string Payload(params string[] values) =>
        string.Concat(values.Select(v => v.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + v));
}
