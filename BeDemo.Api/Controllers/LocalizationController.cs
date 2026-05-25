using BeDemo.Api.Localization;
using BeDemo.Api.Models.Requests.OAuth;
using BeDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Serves static UI translation bundles for portal, admin, and mobile (embedded <c>.resx</c> → JSON for i18next).
/// </summary>
/// <remarks>
/// <para><strong>Routing:</strong> <c>/api/localization</c> is face-prefix exempt (call without <c>/{face}/</c>).</para>
/// <para><strong>Auth:</strong> Anonymous — login/register screens need strings before OAuth tokens exist.</para>
/// <para><strong>Rate limit:</strong> Policy <c>localization-read</c> (IP fixed window). Over-limit → 429,
/// <c>Retry-After</c>, JSON <c>rate_limit</c> (see <c>AddRateLimiter</c> in Program.cs). Config:
/// <c>Localization:RateLimitPermitLimit</c>, <c>Localization:RateLimitWindowSeconds</c>.</para>
/// </remarks>
[ApiController]
[Route("api/localization")]
[AllowAnonymous]
public class LocalizationController : ControllerBase
{
	private readonly ILocalizationBundleService _bundles;

	public LocalizationController(ILocalizationBundleService bundles)
	{
		_bundles = bundles;
	}

	/// <summary>
	/// Returns the full static bundle for <paramref name="app"/> (<c>portal</c>, <c>admin</c>, or <c>mobile</c>).
	/// </summary>
	/// <param name="v">Optional client-known <c>version</c> hash; when it matches the server hash, returns 304.</param>
	[HttpGet("{app}")]
	[EnableRateLimiting("localization-read")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status304NotModified)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
	public IActionResult GetBundle(string app, [FromQuery] LocalizationBundleQuery query)
	{
		if (!LocalizationAppParser.TryParse(app, out var parsed))
			return NotFound(new { error = "unknown_app", app });

		var bundle = _bundles.GetBundle(parsed);
		if (bundle == null)
			return StatusCode(StatusCodes.Status500InternalServerError, new { error = "bundle_empty", app });

		var v = query.V;
		if (!string.IsNullOrEmpty(v) && string.Equals(v, bundle.Version, StringComparison.OrdinalIgnoreCase))
			return StatusCode(StatusCodes.Status304NotModified);

		Response.Headers.CacheControl = "public, max-age=300";
		return Ok(new
		{
			app = bundle.App,
			version = bundle.Version,
			defaultNamespace = bundle.DefaultNamespace,
			supportedLanguages = bundle.SupportedLanguages,
			resources = bundle.Resources,
		});
	}
}
