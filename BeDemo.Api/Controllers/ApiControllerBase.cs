using Microsoft.AspNetCore.Mvc;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Common base for API controllers (backend-refactor X6). Centralises the authenticated caller's user id, which was
/// previously copy-pasted as a private <c>UserId</c> accessor in ~20 controllers. Derive from this instead of
/// <see cref="ControllerBase"/> to inherit <see cref="UserId"/>; everything else (routing, <c>[Authorize]</c>, model
/// binding) is unchanged.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
	/// <summary>
	/// The authenticated caller's stable user id (the <c>NameIdentifier</c> claim), or null when the request is
	/// unauthenticated or the claim is absent. Mirrors the imperative accessor it replaces verbatim.
	/// </summary>
	protected string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}
