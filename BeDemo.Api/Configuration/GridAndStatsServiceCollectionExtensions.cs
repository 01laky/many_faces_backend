using BeDemo.Api.Services;
using BeDemo.Api.Services.Auth;
using BeDemo.Api.Services.Faces;
using BeDemo.Api.Services.Messenger;
using BeDemo.Api.Services.Grid;
using Microsoft.Extensions.DependencyInjection;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Composition-root extension (backend-refactor Phase 3 — Program.cs modularisation) for the performance options and
/// the stats / access-token-version / capabilities / faces-config / grid-list / conversation-list / hub-display
/// service registrations. Moved verbatim; DI resolves order-independently, so behaviour is unchanged.
/// </summary>
public static class GridAndStatsServiceCollectionExtensions
{
	public static IServiceCollection AddManyFacesGridAndStatsServices(this IServiceCollection services)
	{
		services.AddOptions<PerformanceOptions>()
			.BindConfiguration(PerformanceOptions.SectionName);
		services.AddScoped<PlatformStatsQueryService>();
		services.AddScoped<IPlatformStatsQueryService, PlatformStatsCachedQueryService>();
		services.AddSingleton<AccessTokenVersionCacheInterceptor>();
		services.AddScoped<IAccessTokenVersionCache, AccessTokenVersionCache>();
		services.AddScoped<AccessCapabilitiesService>();
		services.AddScoped<IAccessCapabilitiesService, CapabilitiesCacheService>();
		services.AddScoped<IFacesConfigService, FacesConfigService>();
		services.AddScoped<IAlbumGridListService, AlbumGridListService>();
		services.AddScoped<IBlogGridListService, BlogGridListService>();
		services.AddScoped<IReelGridListService, ReelGridListService>();
		services.AddScoped<IStoryGridListService, StoryGridListService>();
		services.AddScoped<IFaceGridSnapshotService, FaceGridSnapshotService>();
		services.AddScoped<IConversationListService, ConversationListService>();
		services.AddScoped<IHubUserDisplayCache, HubUserDisplayCache>();

		return services;
	}
}
