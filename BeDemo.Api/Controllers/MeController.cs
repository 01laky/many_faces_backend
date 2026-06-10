using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Services;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
	private readonly IAccessCapabilitiesService _capabilities;
	private readonly IFaceScopeContext _faceScope;
	private readonly ILogger<MeController> _logger;

	public MeController(
		IAccessCapabilitiesService capabilities,
		IFaceScopeContext faceScope,
		ILogger<MeController> logger)
	{
		_capabilities = capabilities;
		_faceScope = faceScope;
		_logger = logger;
	}

	/// <summary>
	/// GET /api/me/capabilities — computed permissions for the current JWT and URL face scope (A10).
	/// </summary>
	[HttpGet("capabilities")]
	[ProducesResponseType(typeof(CapabilitiesResponse), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetCapabilities(CancellationToken cancellationToken)
	{
		if (!_faceScope.IsAvailable)
		{
			_logger.LogWarning("Capabilities requested without face scope");
			return BadRequest(new ErrorResponseDto { Error = "Face URL prefix is required for capabilities (e.g. /public/api/me/capabilities)." });
		}

		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrEmpty(userId))
			return Unauthorized();

		var dto = await _capabilities.GetCapabilitiesAsync(userId, User, cancellationToken);
		return Ok(dto);
	}
}
