using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Composition-root extension (backend-refactor Phase 3 — Program.cs modularisation) for the operator-AI stack:
/// OperatorAiOptions (validated, X3), the conversation/live-stats/decision services, the RAG retrieval singletons
/// (knowledge client, status cache, planner fallback, retriever, indexer), the routed skills, and the startup hosted
/// services. Registrations are moved verbatim; DI resolves them order-independently, so behaviour is unchanged.
/// </summary>
public static class OperatorAiServiceCollectionExtensions
{
	public static IServiceCollection AddManyFacesOperatorAi(this IServiceCollection services)
	{
		services.AddOptions<BeDemo.Api.Configuration.OperatorAiOptions>()
			.BindConfiguration(BeDemo.Api.Configuration.OperatorAiOptions.SectionName)
			.ValidateOnStart(); // backend-refactor X3 — fail fast on a misconfigured bounded value
		services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<BeDemo.Api.Configuration.OperatorAiOptions>,
			BeDemo.Api.Configuration.OperatorAiOptionsValidator>();
		services.AddScoped<IOperatorAiConversationService, OperatorAiConversationService>();
		services.AddScoped<IOperatorAiEntityBundleLoader, OperatorAiEntityBundleLoader>();
		services.AddScoped<IOperatorAiLiveStatsPrefetcher, OperatorAiLiveStatsPrefetcher>();
		services.AddScoped<IOperatorAiLiveStatsOrchestrator, OperatorAiLiveStatsOrchestrator>();
		// 7B-perf: decision helper (O19 Role A — deterministic + optional helper model), single-active-generation guard
		// (O17, singleton in-process state), and the optional exact-repeat answer cache (O18, singleton over IMemoryCache).
		services.AddScoped<IOperatorAiDecisionHelper, OperatorAiDecisionHelper>();
		services.AddSingleton<IOperatorAiActiveGenerationGuard, OperatorAiActiveGenerationGuard>();
		services.AddSingleton<IOperatorAiAnswerCache, OperatorAiAnswerCache>();

		// ── Operator AI RAG retrieval (operator-ai-rag-retrieval-refactor-v1, §8) ──────
		// Embedding-based semantic retrieval replaces the LLM planner as the bundle
		// SELECTION step; the per-bundle map + stitch is retained. All deps below are
		// singletons (gRPC channel + IMemoryCache + IAiGrpcService), so these are
		// registered as singletons to mirror the search gateway / host-profile lifetimes.
		//
		//   - ISearchWorkerKnowledgeClient : the only door to the operator-ai-knowledge
		//     ES index (IndexKnowledge / SemanticSearch / KnowledgeIndexStatus); owns a
		//     gRPC channel like SearchWorkerGrpcGateway → singleton + IDisposable.
		//   - IOperatorAiKnowledgeStatusCache : short-TTL readiness/health cache (§17.4/§17.9).
		//   - IOperatorAiPlannerFallbackSelector : the legacy planner demoted to fallback (§6/D12).
		//   - IOperatorAiRetriever : EmbedText → SemanticSearch → ordered bundle indices (§8).
		//   - IOperatorAiKnowledgeIndexer : builds + embeds + bulk-upserts the 61 descriptors (§7).
		services.AddSingleton<ISearchWorkerKnowledgeClient, SearchWorkerKnowledgeClient>();
		services.AddSingleton<IOperatorAiKnowledgeStatusCache, OperatorAiKnowledgeStatusCache>();
		services.AddSingleton<IOperatorAiPlannerFallbackSelector, OperatorAiPlannerFallbackSelector>();
		services.AddSingleton<IOperatorAiRetriever, OperatorAiRetriever>();
		services.AddSingleton<IOperatorAiKnowledgeIndexer, OperatorAiKnowledgeIndexer>();

		// Operator AI skills (operator-ai-skills v1): the chat front door routes a request to one skill and runs it.
		//   - Skills live in the backend (worker stays thin); discovery = in-memory cosine over 4 cached descriptor
		//     vectors (D2); single-skill routing → general-assistant fallback below threshold (D5). No per-skill ACL (D10).
		//   - Skills are Scoped (StatsSkill/ReportsSkill/ModerationSkill use scoped orchestrator / metrics); the router is
		//     Scoped, backed by a Singleton vector cache so the 4 descriptors are embedded once, not per request.
		services.AddSingleton<BeDemo.Api.Services.OperatorAi.Skills.IOperatorAiSkillVectorCache, BeDemo.Api.Services.OperatorAi.Skills.OperatorAiSkillVectorCache>();
		services.AddScoped<BeDemo.Api.Services.OperatorAi.Skills.IOperatorAiSkill, BeDemo.Api.Services.OperatorAi.Skills.StatsSkill>();
		services.AddScoped<BeDemo.Api.Services.OperatorAi.Skills.IOperatorAiSkill, BeDemo.Api.Services.OperatorAi.Skills.ReportsSkill>();
		services.AddScoped<BeDemo.Api.Services.OperatorAi.Skills.IOperatorAiSkill, BeDemo.Api.Services.OperatorAi.Skills.ModerationSkill>();
		services.AddScoped<BeDemo.Api.Services.OperatorAi.Skills.IOperatorAiSkill, BeDemo.Api.Services.OperatorAi.Skills.GeneralAssistantSkill>();
		services.AddScoped<BeDemo.Api.Services.OperatorAi.Skills.IOperatorAiSkillRegistry, BeDemo.Api.Services.OperatorAi.Skills.OperatorAiSkillRegistry>();
		services.AddScoped<BeDemo.Api.Services.OperatorAi.Skills.IOperatorAiSkillRouter, BeDemo.Api.Services.OperatorAi.Skills.OperatorAiSkillRouter>();

		// Startup hosted services (§5.5 dim assertion + §7.2 trigger 1 index refresh).
		// Both are non-blocking BackgroundServices that degrade gracefully if the worker
		// is not yet reachable; retrieval falls back to the planner until the index is ready.
		services.AddHostedService<OperatorAiEmbeddingDimStartupAssertion>();
		services.AddHostedService<OperatorAiKnowledgeIndexStartupRefresh>();
		// 7B-perf O8/O10 — warm the 4 skill routing vectors + issue one tiny throwaway Generate at startup so the first
		// operator turn pays neither the descriptor-embed warm nor a cold model load. Non-blocking, AI-gated, best-effort.
		services.AddHostedService<OperatorAiStartupWarmService>();

		return services;
	}
}
