using BeDemo.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Composition-root extension (backend-refactor Phase 3 — Program.cs modularisation) for the platform authorization
/// setup. Bundles the default-deny fallback policy (BSH3-A1), the declarative X5 platform policies, and the scoped
/// <see cref="ManageAllFacesAuthorizationHandler"/>. Behaviour is identical to the inline registration it replaces.
/// </summary>
public static class AuthorizationServiceCollectionExtensions
{
	public static IServiceCollection AddManyFacesAuthorization(this IServiceCollection services)
	{
		// BSH3-A1: default deny — explicit [AllowAnonymous] on OAuth, JWKS, localization, documented public routes.
		services.AddAuthorization(options =>
		{
			options.FallbackPolicy = new AuthorizationPolicyBuilder()
				.RequireAuthenticatedUser()
				.Build();

			// Backend-refactor X5: declarative platform policies (SuperAdmin/GlobalAdmin/ManageAllFaces) mirroring
			// PlatformAccessRules. The per-controller migration onto them is complete.
			PlatformAuthorizationPolicies.Configure(options);
		});

		// X5: ManageAllFaces needs the request-scoped IFaceScopeContext, so it is a scoped handler (not a static assertion).
		services.AddScoped<IAuthorizationHandler, ManageAllFacesAuthorizationHandler>();

		return services;
	}
}
