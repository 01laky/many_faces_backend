using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Auth;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Utils;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Composition-root extension (backend-refactor Phase 3 — Program.cs modularisation) for the OAuth / token-lifetime /
/// uploads / hardened-security / AI-gRPC service registrations (password hasher, IClock, validated JwtTokenLifetime /
/// Uploads / HardenedSecurity / AiService options, the OAuth services, the signed-upload-URL service, and the AI gRPC
/// client + host-profile services). Excludes the ECDSA key + JWT-bearer authentication setup, which is entangled with
/// the request pipeline and stays in Program.cs. Moved verbatim; DI resolves order-independently.
/// </summary>
public static class OAuthAndAiServiceCollectionExtensions
{
	public static IServiceCollection AddManyFacesOAuthAndAiServices(this IServiceCollection services)
	{
		services.AddScoped<IPasswordHasher<OAuthClient>, PasswordHasher<OAuthClient>>();

		services.AddSingleton<IClock, SystemUtcClock>();

		// SHV2 BE-A2: cap remember-me access JWT at 7 days; reject legacy multi-year ExpiresInMinutesRememberMe at startup.
		services.AddOptions<BeDemo.Api.Configuration.JwtTokenLifetimeOptions>()
			.BindConfiguration(BeDemo.Api.Configuration.JwtTokenLifetimeOptions.SectionName)
			.Validate(
				o => o.ExpiresInMinutes > 0 &&
					 o.ExpiresInMinutesRememberMe > 0 &&
					 o.ExpiresInMinutesRememberMe <= BeDemo.Api.Configuration.JwtTokenLifetimeOptions.MaxRememberMeAccessMinutes &&
					 o.ExpiresInMinutesRememberMe >= o.ExpiresInMinutes,
				$"Jwt:{nameof(BeDemo.Api.Configuration.JwtTokenLifetimeOptions.ExpiresInMinutesRememberMe)} must be " +
				$"{BeDemo.Api.Configuration.JwtTokenLifetimeOptions.RecommendedRememberMeAccessMinutes} minutes (7 days) or less, " +
				"and not less than Jwt:ExpiresInMinutes. Remove legacy values like " +
				$"{BeDemo.Api.Configuration.JwtTokenLifetimeOptions.LegacyMisconfiguredRememberMeMinutes}.")
			.ValidateOnStart();

		services.AddScoped<IOAuthClientValidator, OAuthClientValidator>();
		services.AddScoped<IOAuthTokenRequestSignatureVerifier, OAuthTokenRequestSignatureVerifier>();
		services.AddScoped<IOAuthAccessTokenFactory, OAuthAccessTokenFactory>();
		services.AddScoped<IOAuth2Service, OAuth2Service>();

		// SHV2 BE-U3 — HMAC-signed URLs for wwwroot/uploads (replaces public static /uploads/*).
		services.AddOptions<BeDemo.Api.Configuration.UploadsOptions>()
			.BindConfiguration(BeDemo.Api.Configuration.UploadsOptions.SectionName)
			.Validate(
				o => !string.IsNullOrWhiteSpace(o.SigningSecret) && o.SigningSecret.Length >= 32,
				$"Uploads:{nameof(BeDemo.Api.Configuration.UploadsOptions.SigningSecret)} must be at least 32 characters.")
			.ValidateOnStart();
		services.AddSingleton<IUploadSignedUrlService, UploadSignedUrlService>();

		services.AddOptions<HardenedSecurityOptions>()
			.BindConfiguration(HardenedSecurityOptions.SectionName)
			.ValidateOnStart();
		services.AddSingleton<IValidateOptions<HardenedSecurityOptions>, HardenedSecurityValidateOptions>();

		// AI gRPC client - singleton to reuse the HTTP/2 channel across requests
		services.AddOptions<AiServiceOptions>()
			.BindConfiguration(AiServiceOptions.SectionName)
			.ValidateOnStart(); // backend-refactor X3
		services.AddSingleton<IValidateOptions<AiServiceOptions>, AiServiceOptionsValidator>();
		services.AddSingleton<AiGrpcService>();
		services.AddSingleton<IAiModelStatusClient>(sp => sp.GetRequiredService<AiGrpcService>());
		services.AddSingleton<IAiGrpcService, AiAvailabilityGuardGrpcService>();
		services.AddScoped<IAiWorkerHostProfileService, AiWorkerHostProfileService>();
		services.AddHostedService<AiWorkerHostProfileStartupRefresh>();

		return services;
	}
}
