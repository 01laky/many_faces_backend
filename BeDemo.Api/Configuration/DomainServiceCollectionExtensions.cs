using BeDemo.Api.Services;
using BeDemo.Api.Services.Auth;
using BeDemo.Api.Services.Faces;
using Microsoft.Extensions.DependencyInjection;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Composition-root extension (backend-refactor Phase 3 — Program.cs modularisation) for core per-request domain
/// services: face lookup + profile-detail pages, the HTTP context accessor, the request face scope and access
/// evaluator, the OAuth refresh-token store, the chat-hub AI rate limiter, and the story / face-wall-ticket lifecycle
/// services. Moved verbatim; DI resolves order-independently, so behaviour is unchanged.
/// </summary>
public static class DomainServiceCollectionExtensions
{
	public static IServiceCollection AddManyFacesDomainServices(this IServiceCollection services)
	{
		services.AddScoped<IFaceService, FaceService>();
		services.AddScoped<IProfileDetailTemplatePagesService, ProfileDetailTemplatePagesService>();
		services.AddHttpContextAccessor();
		services.AddScoped<IFaceScopeContext, FaceScopeContext>();
		services.AddScoped<IAccessEvaluator, AccessEvaluator>();
		services.AddScoped<IOAuthRefreshTokenStore, OAuthRefreshTokenStore>();
		services.AddSingleton<IChatHubAiRateLimiter, ChatHubAiRateLimiter>();
		services.AddScoped<IStoryLifecycleService, StoryLifecycleService>();
		services.AddScoped<IFaceWallTicketLifecycleService, FaceWallTicketLifecycleService>();

		return services;
	}
}
