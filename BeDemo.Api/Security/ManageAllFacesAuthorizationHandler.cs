using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using Microsoft.AspNetCore.Authorization;

namespace BeDemo.Api.Security;

/// <summary>
/// X5 handler for <see cref="ManageAllFacesRequirement"/>: succeeds when the current request is admin-face-scoped AND
/// the principal is a global super-admin — i.e. exactly <see cref="PlatformAccessRules.CanManageAllFaces"/>. The
/// requirement needs the per-request <see cref="IFaceScopeContext"/> (which face the path resolved to), so unlike the
/// claims-only SuperAdmin/GlobalAdmin policies it cannot be expressed as a static <c>RequireAssertion</c>; it is a
/// scoped handler. Registered DI-scoped because <see cref="IFaceScopeContext"/> is request-scoped.
/// </summary>
public sealed class ManageAllFacesAuthorizationHandler : AuthorizationHandler<ManageAllFacesRequirement>
{
	private readonly IFaceScopeContext _faceScope;

	public ManageAllFacesAuthorizationHandler(IFaceScopeContext faceScope)
	{
		_faceScope = faceScope;
	}

	protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ManageAllFacesRequirement requirement)
	{
		// Mirror the imperative gate verbatim — admin face scope + global super-admin. No Fail() call: leaving the
		// requirement unmet lets other handlers/policies decide, matching ASP.NET's additive evaluation model.
		if (PlatformAccessRules.CanManageAllFaces(_faceScope, context.User))
		{
			context.Succeed(requirement);
		}

		return Task.CompletedTask;
	}
}
