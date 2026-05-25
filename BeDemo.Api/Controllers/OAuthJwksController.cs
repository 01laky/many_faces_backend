using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Publishes public JWK set for JWT signature verification (multi-instance API / gateways). Path is exempt from face prefix (<c>/api/oauth2/*</c>).
/// </summary>
[ApiController]
[Route("api/oauth2")]
[AllowAnonymous]
public sealed class OAuthJwksController : ControllerBase
{
	/// <summary>
	/// GET /api/oauth2/jwks — JSON Web Key Set for the current signing key (ES512 / P-521).
	/// </summary>
	[HttpGet("jwks")]
	public IActionResult GetJwks([FromServices] IECDSAKeyService keys)
	{
		var jwks = new List<JsonWebKey>();
		foreach (var sk in keys.GetIssuerSigningKeys())
		{
			if (sk is ECDsaSecurityKey ek)
				jwks.Add(JsonWebKeyConverter.ConvertFromECDsaSecurityKey(ek));
		}

		var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
		return new JsonResult(new { keys = jwks.ToArray() }, options);
	}
}
