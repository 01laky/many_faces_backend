using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Composition-root extension (backend-refactor Phase 3 — Program.cs modularisation) for platform-wide options binding
/// (content-moderation security, search, push/Firebase, mail, registration-invite) and the registration-invite,
/// search (outbox/gateway/ACL/reconciliation, with the config-gated hosted services) and push/mailer worker-client
/// registrations. Moved verbatim; DI resolves order-independently, so behaviour is unchanged.
/// </summary>
public static class PlatformServiceCollectionExtensions
{
	public static IServiceCollection AddManyFacesPlatformServices(this IServiceCollection services, IConfiguration configuration)
	{
		services.Configure<ContentModerationSecurityOptions>(
			configuration.GetSection(ContentModerationSecurityOptions.SectionName));
		services.Configure<SearchOptions>(configuration.GetSection(SearchOptions.SectionName));
		services.Configure<PushOptions>(configuration.GetSection(PushOptions.SectionName));
		services.Configure<PushFirebaseBootstrapOptions>(
			configuration.GetSection($"{PushOptions.SectionName}:Firebase"));
		services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));
		services.Configure<RegistrationInviteOptions>(configuration.GetSection(RegistrationInviteOptions.SectionName));
		services.Configure<MailRegistrationLinkOptions>(configuration.GetSection(MailRegistrationLinkOptions.SectionName));
		services.AddScoped<IRegistrationInviteService, RegistrationInviteService>();
		services.AddScoped<IUserRegistrationProvisioner, UserRegistrationProvisioner>();
		services.AddHostedService<RegistrationInviteCleanupHostedService>();
		services.AddHttpClient();
		services.AddSingleton<SearchOutboxSaveChangesInterceptor>();
		services.AddSingleton<ISearchWorkerProbe, SearchWorkerGrpcProbe>();
		services.AddSingleton<ISearchQueryGateway, SearchWorkerGrpcGateway>();
		services.AddScoped<ISearchOutboxService, SearchOutboxService>();
		services.AddScoped<SearchDocumentBuilder>();
		services.AddScoped<SearchHitAclFilter>();
		services.AddScoped<SearchHitBatchFilter>();
		services.AddScoped<IAdminSearchAutocompleteService, AdminSearchAutocompleteService>();
		services.AddScoped<SearchIndexReconciliationRunner>();

		var searchOptionsForHosted = configuration.GetSection(SearchOptions.SectionName).Get<SearchOptions>() ?? new SearchOptions();
		if (searchOptionsForHosted.IsEnabled)
		{
			services.AddHostedService<SearchOutboxProcessorHostedService>();
			if (searchOptionsForHosted.ReconciliationEnabled)
				services.AddHostedService<SearchIndexReconciliationHostedService>();
		}

		services.AddSingleton<IPushWorkerClient, PushWorkerGrpcClient>();
		services.AddSingleton<IMailerWorkerClient, MailerWorkerGrpcClient>();

		return services;
	}
}
