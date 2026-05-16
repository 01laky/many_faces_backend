using BeDemo.Api.Localization;
using BeDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Serves static UI translation bundles for portal, admin, and mobile (from embedded .resx).
/// </summary>
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

    /// <summary>GET /api/localization/{app} — app is portal, admin, or mobile.</summary>
    [HttpGet("{app}")]
    [EnableRateLimiting("localization-read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetBundle(string app, [FromQuery] string? v)
    {
        if (!LocalizationAppParser.TryParse(app, out var parsed))
            return NotFound(new { error = "unknown_app", app });

        var bundle = _bundles.GetBundle(parsed);
        if (bundle == null)
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "bundle_empty", app });

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
