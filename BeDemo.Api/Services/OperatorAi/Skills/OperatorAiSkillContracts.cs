using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Services.OperatorAi.Skills;

/// <summary>
/// Whether a skill's assembled data is safe to put in a prompt as-is, or is untrusted user content that must be
/// sanitized + delimited (§7). v1 skills are all <see cref="Trusted"/> (aggregates only); the enum exists so the
/// deferred untrusted moderation-excerpt path can declare <see cref="Untrusted"/> without an interface change.
/// </summary>
public enum OperatorAiSkillTrust
{
	Trusted,
	Untrusted,
}

/// <summary>
/// Per-turn input handed to a skill. PURE DATA — shared services are constructor-injected into each skill, not
/// passed here (no service-locator). <see cref="RoutingScore"/> is the cosine score that selected the skill.
/// </summary>
public sealed record OperatorAiSkillRequest(
	string UserMessage,
	int ConversationId,
	IReadOnlyList<ChatHistoryEntry> RecentHistory,
	int? MaxParallelBundleAiCalls,
	double RoutingScore);

/// <summary>One operator-visible answer + optional structured payload + trace (§3).</summary>
public sealed record OperatorAiSkillResult(
	string AnswerMarkdown,
	object? StructuredPayload = null,
	OperatorAiSkillTrace? Trace = null);

/// <summary>
/// Skill trace for observability / dev debug (which skill ran, whether it retrieved, fallback, timing). The 7B-perf
/// fields (O9) record which fast-path fired, how many LLM generations the turn actually used, per-stage latencies,
/// and whether the CPU-resident decision helper (O19) was consulted.
/// </summary>
public sealed record OperatorAiSkillTrace(
	string SkillId,
	bool UsedRetrieval,
	bool FellBackInternally,
	long ElapsedMs,
	string? FastPath = null,
	int Generations = 0,
	long LoadMs = 0,
	long MapMs = 0,
	bool HelperUsed = false,
	// operator-ai degraded-handling D10 — the turn ran but ≥1 data section could not be produced (model down/timed out).
	bool Degraded = false);

/// <summary>
/// 7B-perf O4 — one chunk from a streaming skill run. During generation, <see cref="Delta"/> carries incremental
/// text and <see cref="IsFinal"/> is false. The terminal chunk has <see cref="IsFinal"/> true plus the full
/// <see cref="FinalAnswer"/> (what the backend persists) and the <see cref="Trace"/>.
/// </summary>
public sealed record OperatorAiStreamChunk(
	string? Delta,
	bool IsFinal,
	string? FinalAnswer = null,
	OperatorAiSkillTrace? Trace = null);

/// <summary>
/// A named, discoverable operator-AI capability (§3). Identity metadata (<see cref="Id"/>/<see cref="Description"/>/
/// <see cref="SampleRequests"/>) is embedded by the router for cosine routing; <see cref="RunAsync"/> does the work
/// via constructor-injected services. One skill runs per operator turn (v1, D3). No ACL member — all AI features
/// are SUPER_ADMIN-only and gated once at the chat front door (D10).
/// </summary>
public interface IOperatorAiSkill
{
	/// <summary>Stable id, registry key, e.g. "stats".</summary>
	string Id { get; }

	/// <summary>Human label for traces / future admin surfaces.</summary>
	string DisplayName { get; }

	/// <summary>"Use this when the operator asks about …" — embedded for routing.</summary>
	string Description { get; }

	/// <summary>Example operator asks — concatenated into the routing embedding to improve recall.</summary>
	IReadOnlyList<string> SampleRequests { get; }

	/// <summary>
	/// One terse line for the 3B LLM router's single-label classification (operator-ai LLM skill router). Kept short
	/// on purpose — the verbose <see cref="Description"/> + <see cref="SampleRequests"/> bloat the classifier prompt
	/// and hurt accuracy. Used only by the router's helper classification, not by the cosine fallback.
	/// </summary>
	string RouterHint { get; }

	/// <summary>Governs prompt assembly (sanitize when <see cref="OperatorAiSkillTrust.Untrusted"/>).</summary>
	OperatorAiSkillTrust Trust { get; }

	/// <summary>Run the skill for one operator turn and return a single answer.</summary>
	Task<OperatorAiSkillResult> RunAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// 7B-perf O4 — implemented by skills whose terminal step is a single operator-visible generation that can stream
/// token by token (stats, moderation, general-assistant). The ChatHub forwards each <see cref="OperatorAiStreamChunk.Delta"/>
/// to the admin as a SignalR delta event, then persists the final <see cref="OperatorAiStreamChunk.FinalAnswer"/> once.
/// Skills without a terminal generation (reports — deterministic markdown) do not implement this and use the
/// non-streaming <see cref="IOperatorAiSkill.RunAsync"/>.
/// </summary>
public interface IOperatorAiStreamingSkill : IOperatorAiSkill
{
	/// <summary>Run the skill streaming the terminal generation; the last chunk carries the full answer + trace.</summary>
	IAsyncEnumerable<OperatorAiStreamChunk> RunStreamingAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// DI collects every registered <see cref="IOperatorAiSkill"/>; the registry lists them, resolves by id, and
/// exposes the guaranteed <see cref="GeneralAssistant"/> fallback (D5). The router uses it both to warm routing
/// vectors and to resolve the selected / fallback skill.
/// </summary>
public interface IOperatorAiSkillRegistry
{
	IReadOnlyList<IOperatorAiSkill> All { get; }
	IOperatorAiSkill? GetById(string id);
	IOperatorAiSkill GeneralAssistant { get; }
}
