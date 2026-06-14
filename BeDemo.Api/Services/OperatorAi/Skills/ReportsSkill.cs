using System.Diagnostics;
using System.Text.Json;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi.Skills;

/// <summary>
/// Structured admin report skill (§6.2) — v1 is simple/deterministic: detect the report type from the operator
/// message, assemble a small <c>input_json</c> from existing aggregate data, and call the worker
/// <c>GenerateReport</c> RPC (3 shipped types: face_health, moderation_backlog, grid_completeness). Figures come
/// from the assembled data — the worker renders deterministic markdown, it does not invent numbers. Trusted (no
/// raw content). On-demand only; no AI insight layer, no scheduling, no persistence in v1 (all deferred).
/// </summary>
public sealed class ReportsSkill : IOperatorAiSkill
{
	private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	private readonly IAiGrpcService _ai;
	private readonly IContentModerationMetrics _metrics;
	private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
	private readonly IOperatorAiDecisionHelper _decisions;
	private readonly OperatorAiOptions _options;

	public ReportsSkill(
		IAiGrpcService ai,
		IContentModerationMetrics metrics,
		IDbContextFactory<ApplicationDbContext> dbFactory,
		IOperatorAiDecisionHelper decisions,
		IOptions<OperatorAiOptions> options)
	{
		_ai = ai;
		_metrics = metrics;
		_dbFactory = dbFactory;
		_decisions = decisions;
		_options = options.Value;
	}

	public string Id => "reports";
	public string DisplayName => "Admin reports";

	public string Description =>
		"Generate a structured admin report — face health, the content-moderation backlog, or grid/component "
		+ "completeness. Use this when the operator asks to produce or generate a report.";

	public IReadOnlyList<string> SampleRequests =>
		[
			"generate a moderation backlog report",
			"produce a face health report",
			"grid completeness report",
			"give me an admin report on the moderation queue",
		];

	public string RouterHint =>
		"GENERATE / produce a structured admin REPORT document (face health, moderation backlog, or grid "
		+ "completeness) — only when the operator explicitly asks to generate/produce a report";

	public OperatorAiSkillTrust Trust => OperatorAiSkillTrust.Trusted;

	public async Task<OperatorAiSkillResult> RunAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();

		// O19 Role A — the helper refines the type when configured; otherwise the deterministic heuristic decides.
		var reportType = await _decisions.DetectReportTypeAsync(request.UserMessage, cancellationToken);
		if (reportType is null)
		{
			sw.Stop();
			return new OperatorAiSkillResult(
				"I can generate these reports: **face health**, **moderation backlog**, and **grid completeness**. "
				+ "Which one would you like?",
				Trace: new OperatorAiSkillTrace(Id, UsedRetrieval: false, FellBackInternally: true, sw.ElapsedMilliseconds));
		}

		var inputJson = await BuildInputJsonAsync(reportType, cancellationToken);

		var result = await _ai.GenerateReportAsync(reportType, inputJson, _options.LiveStitchMaxNewTokens, cancellationToken);

		sw.Stop();
		if (!result.HasReport)
		{
			return new OperatorAiSkillResult(
				"Sorry — the report could not be generated right now. Please try again shortly.",
				Trace: new OperatorAiSkillTrace(Id, UsedRetrieval: false, FellBackInternally: true, sw.ElapsedMilliseconds));
		}

		return new OperatorAiSkillResult(
			result.Markdown!,
			StructuredPayload: result.ReportJson,
			Trace: new OperatorAiSkillTrace(Id, UsedRetrieval: false, FellBackInternally: false, sw.ElapsedMilliseconds));
	}

	/// <summary>Map the message to one of the 3 supported report types, or null when ambiguous (→ help message). Deterministic heuristic shared with the decision helper (O19) as its fallback.</summary>
	internal static string? DetectReportType(string message) => OperatorAiReportTypeHeuristic.Detect(message);

	/// <summary>Assemble the deterministic input_json for the report type from existing aggregate data (camelCase keys).</summary>
	private async Task<string> BuildInputJsonAsync(string reportType, CancellationToken cancellationToken)
	{
		switch (reportType)
		{
			case "moderation_backlog":
				{
					var snap = await _metrics.GetSnapshotAsync(cancellationToken);
					return JsonSerializer.Serialize(
						new { pendingCount = snap.PendingSubmissions, oldestHours = snap.OldestPendingAgeHours ?? 0d },
						Json);
				}
			case "grid_completeness":
				{
					await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
					var componentTypeCount = await db.ComponentTypes.AsNoTracking().CountAsync(cancellationToken);
					return JsonSerializer.Serialize(
						new { componentTypeCount, missingTypes = Array.Empty<string>() },
						Json);
				}
			case "face_health":
			default:
				{
					// v1 is platform-level (a per-face report is deferred): totals across all faces.
					await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
					var faceCount = await db.Faces.AsNoTracking().CountAsync(cancellationToken);
					var pageCount = await db.Pages.AsNoTracking().CountAsync(cancellationToken);
					return JsonSerializer.Serialize(
						new { face = new { title = $"All faces ({faceCount})", isPublic = true }, pageCount },
						Json);
				}
		}
	}
}
