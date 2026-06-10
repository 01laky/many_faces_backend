using System.Threading.RateLimiting;
using BeDemo.Api.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Composition-root extension (backend-refactor Phase 3 — Program.cs modularisation) for the rate-limiter setup:
/// reads the OAuth/register/localization/login/api/upload/prefill/ai-availability/signalr permit + window limits
/// (with the testing bypass), then registers the partitioned named policies. Behaviour is identical to the inline
/// block it replaces; the testing bypass is driven by <paramref name="isTestingEnv"/> exactly as before.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
	public static IServiceCollection AddManyFacesRateLimiting(this IServiceCollection services, IConfiguration configuration, bool isTestingEnv)
	{
		var bypassRateLimitInTesting = configuration.GetValue("OAuth2:BypassRateLimitInTesting", true);
		var oauthPermit = isTestingEnv && bypassRateLimitInTesting
			? 1_000_000
			: configuration.GetValue("OAuth2:TokenRateLimitPermitLimit", 60);
		var oauthWindowSec = configuration.GetValue("OAuth2:TokenRateLimitWindowSeconds", 60);
		var registerPermit = isTestingEnv && bypassRateLimitInTesting
			? 1_000_000
			: configuration.GetValue("OAuth2:RegisterRateLimitPermitLimit", 30);
		var registerWindowSec = configuration.GetValue("OAuth2:RegisterRateLimitWindowSeconds", 60);
		// localization-read: anonymous GET /api/localization/{app} on every SPA cold load.
		// Shares OAuth2:BypassRateLimitInTesting in Testing so most integration tests stay unlimited;
		// LocalizationRateLimit429Tests sets BypassRateLimitInTesting=false and low Localization:* limits.
		var localizationPermit = isTestingEnv && bypassRateLimitInTesting
			? 1_000_000
			: configuration.GetValue("Localization:RateLimitPermitLimit", 120);
		var localizationWindowSec = configuration.GetValue("Localization:RateLimitWindowSeconds", 60);
		var authLoginPermit = isTestingEnv && bypassRateLimitInTesting
			? 1_000_000
			: configuration.GetValue("Auth:LoginRateLimitPermitLimit", 30);
		var authLoginWindowSec = configuration.GetValue("Auth:LoginRateLimitWindowSeconds", 60);
		var apiGlobalPermit = isTestingEnv && bypassRateLimitInTesting
			? 1_000_000
			: configuration.GetValue("RateLimit:ApiPermitLimit", 600);
		var apiGlobalWindowSec = configuration.GetValue("RateLimit:ApiWindowSeconds", 60);
		var uploadPermit = isTestingEnv && bypassRateLimitInTesting
			? 1_000_000
			: configuration.GetValue("RateLimit:UploadPermitLimit", 20);
		var uploadWindowSec = configuration.GetValue("RateLimit:UploadWindowSeconds", 60);
		var registerPrefillPermit = isTestingEnv && bypassRateLimitInTesting
			? 1_000_000
			: configuration.GetValue("RateLimit:RegisterPrefillPermitLimit", 30);
		var registerPrefillWindowSec = configuration.GetValue("RateLimit:RegisterPrefillWindowSeconds", 60);
		var aiAvailabilityPermit = isTestingEnv && bypassRateLimitInTesting
			? 1_000_000
			: configuration.GetValue("RateLimit:AiAvailabilityPermitLimit", 120);
		var aiAvailabilityWindowSec = configuration.GetValue("RateLimit:AiAvailabilityWindowSeconds", 60);
		var signalrNegotiatePermit = isTestingEnv && bypassRateLimitInTesting
			? 1_000_000
			: configuration.GetValue("RateLimit:SignalrNegotiatePermitLimit", 60);
		var signalrNegotiateWindowSec = configuration.GetValue("RateLimit:SignalrNegotiateWindowSeconds", 60);
		services.AddRateLimiter(options =>
		{
			options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
			options.OnRejected = async (ctx, ct) =>
			{
				ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
				if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
					ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
				await ctx.HttpContext.Response.WriteAsync(
					"{\"error\":\"rate_limit\",\"error_description\":\"Too many requests. See Retry-After.\"}",
					ct);
			};
			options.AddPolicy("oauth-token", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = oauthPermit,
						Window = TimeSpan.FromSeconds(Math.Max(1, oauthWindowSec)),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0,
					}));
			options.AddPolicy("oauth-register", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = registerPermit,
						Window = TimeSpan.FromSeconds(Math.Max(1, registerWindowSec)),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0,
					}));
			// Static i18n bundles: separate policy from oauth-* so tuning does not affect login/register.
			options.AddPolicy("localization-read", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = localizationPermit,
						Window = TimeSpan.FromSeconds(Math.Max(1, localizationWindowSec)),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0,
					}));
			options.AddPolicy("auth-login", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = authLoginPermit,
						Window = TimeSpan.FromSeconds(Math.Max(1, authLoginWindowSec)),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0,
					}));
			options.AddPolicy("api-global", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = apiGlobalPermit,
						Window = TimeSpan.FromSeconds(Math.Max(1, apiGlobalWindowSec)),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0,
					}));
			options.AddPolicy("upload-write", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = uploadPermit,
						Window = TimeSpan.FromSeconds(Math.Max(1, uploadWindowSec)),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0,
					}));
			options.AddPolicy("oauth-register-prefill", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = registerPrefillPermit,
						Window = TimeSpan.FromSeconds(Math.Max(1, registerPrefillWindowSec)),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0,
					}));
			options.AddPolicy("ai-availability-read", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = aiAvailabilityPermit,
						Window = TimeSpan.FromSeconds(Math.Max(1, aiAvailabilityWindowSec)),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0,
					}));
			options.AddPolicy("signalr-negotiate", context =>
				RateLimitPartition.GetFixedWindowLimiter(
					partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
					factory: _ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = signalrNegotiatePermit,
						Window = TimeSpan.FromSeconds(Math.Max(1, signalrNegotiateWindowSec)),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0,
					}));

			// BSH3-A4: default API throttle — skipped when endpoint has its own [EnableRateLimiting] policy.
			options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
			{
				var endpoint = httpContext.GetEndpoint();
				if (endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>() != null
					|| endpoint?.Metadata.GetMetadata<DisableRateLimitingAttribute>() != null)
				{
					return RateLimitPartition.GetNoLimiter(RateLimitingPartitionKey.ForHttpContext(httpContext));
				}

				return RateLimitPartition.GetFixedWindowLimiter(
					RateLimitingPartitionKey.ForHttpContext(httpContext),
					_ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = apiGlobalPermit,
						Window = TimeSpan.FromSeconds(Math.Max(1, apiGlobalWindowSec)),
						QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
						QueueLimit = 0,
					});
			});
		});


		return services;
	}
}
