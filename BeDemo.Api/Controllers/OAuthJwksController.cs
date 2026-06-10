using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>JWKS response envelope — wraps the keys array for <c>GET /api/oauth2/jwks</c>.</summary>
file sealed class JwksResult { public required IReadOnlyList<JsonWebKey> Keys { get; init; } }

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
	[ProducesResponseType(StatusCodes.Status200OK)]
	public IActionResult GetJwks([FromServices] IECDSAKeyService keys)
	{
		var jwks = new List<JsonWebKey>();
		foreach (var sk in keys.GetIssuerSigningKeys())
		{
			if (sk is ECDsaSecurityKey ek)
				jwks.Add(JsonWebKeyConverter.ConvertFromECDsaSecurityKey(ek));
		}

		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		};
		return new JsonResult(new JwksResult { Keys = jwks.ToArray() }, options);
	}
}
