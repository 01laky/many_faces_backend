namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Prefetch lifecycle for one catalog index (stage 1 — see ChatHub live pipeline §4).</summary>
public enum OperatorAiBundleCacheState
{
    Loading,
    Ready,
    Failed,
}

/// <summary>In-memory cache row for a single entity bundle during one live request.</summary>
public sealed record OperatorAiBundleCacheEntry(
    int Index,
    string BundleId,
    OperatorAiBundleCacheState State,
    string? JsonPayload,
    string? ErrorMessage,
    DateTime StartedUtc,
    DateTime? CompletedUtc);
