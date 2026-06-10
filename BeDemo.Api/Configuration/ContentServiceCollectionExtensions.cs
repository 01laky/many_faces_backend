using BeDemo.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Composition-root extension (backend-refactor Phase 3 — Program.cs modularisation) for the operator content,
/// moderation, messaging, video-lounge and content-retention services (incl. the validated VideoLoungeOptions, X3,
/// and the content-retention hosted service). Registrations are moved verbatim; DI resolves them order-independently.
/// </summary>
public static class ContentServiceCollectionExtensions
{
	public static IServiceCollection AddManyFacesContentAndModerationServices(this IServiceCollection services)
	{
		services.AddScoped<IFaceModerationService, FaceModerationService>();
		services.AddScoped<IOperatorUserModerationService, OperatorUserModerationService>();
		services.AddScoped<IAdminMeProfileService, AdminMeProfileService>();
		services.AddScoped<IOperatorAlbumManagementService, OperatorAlbumManagementService>();
		services.AddScoped<IOperatorReelManagementService, OperatorReelManagementService>();
		services.AddScoped<IOperatorBlogManagementService, OperatorBlogManagementService>();
		services.AddScoped<IOperatorStoryManagementService, OperatorStoryManagementService>();
		services.AddScoped<IOperatorChatRoomManagementService, OperatorChatRoomManagementService>();
		services.AddScoped<IOperatorProfileSocialManagementService, OperatorProfileSocialManagementService>();
		services.AddScoped<IPlatformDirectMessageService, PlatformDirectMessageService>();
		services.AddScoped<IOperatorUserChatService, OperatorUserChatService>();
		services.AddSingleton<IPlatformChatRateLimiter, PlatformChatRateLimiter>();
		services.AddScoped<IChatRoomLifecycleService, ChatRoomLifecycleService>();
		services.AddOptions<BeDemo.Api.Configuration.VideoLoungeOptions>()
			.BindConfiguration(BeDemo.Api.Configuration.VideoLoungeOptions.SectionName)
			.ValidateOnStart(); // backend-refactor X3
		services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<BeDemo.Api.Configuration.VideoLoungeOptions>,
			BeDemo.Api.Configuration.VideoLoungeOptionsValidator>();
		services.AddScoped<IVideoLoungeTokenService, VideoLoungeTokenService>();
		services.AddScoped<IVideoLoungeLifecycleService, VideoLoungeLifecycleService>();
		// User-generated content moderation: AI job worker, dashboard metrics, in-app notifications, and optional retention cleanup.
		services.AddScoped<IContentAiReviewService, ContentAiReviewService>();
		services.AddScoped<IContentModerationMetrics, ContentModerationMetrics>();
		services.AddScoped<IContentModerationNotifier, ContentModerationNotifier>();
		services.AddScoped<IContentRetentionCleanupService, ContentRetentionCleanupService>();
		services.AddHostedService<ContentRetentionHostedService>();

		return services;
	}
}
