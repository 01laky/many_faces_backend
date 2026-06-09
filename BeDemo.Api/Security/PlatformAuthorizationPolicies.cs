using BeDemo.Api.Utils;
using Microsoft.AspNetCore.Authorization;

namespace BeDemo.Api.Security;

/// <summary>
/// Declarative authorization policies (backend-refactor X5) that mirror the imperative <see cref="PlatformAccessRules"/>
/// checks, so controllers can move from in-body <c>if (!_access.IsGlobalSuperAdmin(User)) return Forbid();</c> to
/// <c>[Authorize(Policy = …)]</c>. Declaring the policies is behaviour-preserving — nothing enforces them until a
/// controller opts in; the per-controller migration follows (ADR 0001 / prompt §10.3: keep the imperative check until
/// each policy has a passing negative test, and never delete a gate and add a policy in the same commit).
/// </summary>
public static class PlatformAuthorizationPolicies
{
	/// <summary>Global SUPER_ADMIN (claims-based) — operator/admin-only surfaces.</summary>
	public const string SuperAdmin = "SuperAdmin";

	/// <summary>Global ADMIN or SUPER_ADMIN (claims-based) — portal admin surfaces.</summary>
	public const string GlobalAdmin = "GlobalAdmin";

	/// <summary>Admin face scope AND global SUPER_ADMIN — the platform-operator gate (needs the request face scope).</summary>
	public const string ManageAllFaces = "ManageAllFaces";

	/// <summary>Register the three platform policies on the given options (call from <c>AddAuthorization</c>).</summary>
	public static void Configure(AuthorizationOptions options)
	{
		options.AddPolicy(SuperAdmin, p => p.RequireAssertion(ctx => PlatformAccessRules.IsGlobalSuperAdmin(ctx.User)));
		options.AddPolicy(GlobalAdmin, p => p.RequireAssertion(ctx => PlatformAccessRules.IsGlobalAdmin(ctx.User)));
		options.AddPolicy(ManageAllFaces, p => p.Requirements.Add(new ManageAllFacesRequirement()));
	}
}

/// <summary>X5 — requirement satisfied by <see cref="ManageAllFacesAuthorizationHandler"/> (admin face scope + super-admin).</summary>
public sealed class ManageAllFacesRequirement : IAuthorizationRequirement;
