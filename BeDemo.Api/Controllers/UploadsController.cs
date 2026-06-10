using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

/// <summary>
/// SHV2 BE-U3 — authenticated download proxy via HMAC-signed URLs (no anonymous <c>/uploads/*</c> static files).
/// </summary>
[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
	private readonly IUploadSignedUrlService _signedUrls;
	private readonly IWebHostEnvironment _env;
	private readonly ILogger<UploadsController> _logger;
	private readonly PerformanceOptions _perfOptions;
	private static readonly FileExtensionContentTypeProvider ContentTypes = new();

	public UploadsController(
		IUploadSignedUrlService signedUrls,
		IWebHostEnvironment env,
		ILogger<UploadsController> logger,
		IOptions<PerformanceOptions> perfOptions)
	{
		_signedUrls = signedUrls;
		_env = env;
		_logger = logger;
		_perfOptions = perfOptions.Value;
	}

	/// <summary>
	/// GET /api/uploads/serve?path=/uploads/...&amp;exp=unix&amp;sig=base64url
	/// Serves a file from wwwroot when the HMAC and expiry are valid.
	/// </summary>
	[HttpGet("serve")]
	[AllowAnonymous]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public IActionResult Serve([FromQuery] string path, [FromQuery] long exp, [FromQuery] string sig)
	{
		if (!_signedUrls.TryValidateServeRequest(path, exp, sig, out var storedPath, out var error))
			return Unauthorized(new ErrorResponseDto { Error = error ?? "Invalid upload URL" });

		var relative = storedPath!.TrimStart('/');
		var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length < 2)
			return NotFound();

		var fileName = segments[^1];
		var dirSegments = segments[..^1];

		var webRoot = string.IsNullOrEmpty(_env.WebRootPath)
			? Path.Combine(_env.ContentRootPath, "wwwroot")
			: _env.WebRootPath;

		if (!UploadPathSecurity.TryResolveFileUnderWebRoot(
				webRoot,
				dirSegments,
				fileName,
				out var fullPath,
				out _))
			return NotFound();

		if (!System.IO.File.Exists(fullPath))
			return NotFound();

		if (!ContentTypes.TryGetContentType(fullPath, out var contentType))
			contentType = "application/octet-stream";

		var maxAge = Math.Max(0, _perfOptions.UploadServeCacheMaxAgeSeconds);
		var remaining = exp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var effectiveMaxAge = remaining > 0 ? Math.Min(maxAge, remaining) : 0;
		if (effectiveMaxAge > 0)
			Response.Headers.CacheControl = $"private, max-age={effectiveMaxAge}";

		var etag = $"\"{storedPath}:{new FileInfo(fullPath).LastWriteTimeUtc.Ticks}\"";
		Response.Headers.ETag = etag;
		if (Request.Headers.IfNoneMatch.Contains(etag))
			return StatusCode(StatusCodes.Status304NotModified);

		return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
	}
}
