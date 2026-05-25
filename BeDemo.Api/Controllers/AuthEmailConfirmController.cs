using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>Anonymous email confirmation links for admin-face operators (v1.2 profile flow).</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthEmailConfirmController : ControllerBase
{
    private readonly IAdminMeProfileService _profiles;

    public AuthEmailConfirmController(IAdminMeProfileService profiles) => _profiles = profiles;

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromQuery] ConfirmEmailQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.UserId) || string.IsNullOrWhiteSpace(query.Token))
            return BadRequest(new { error = "Invalid confirmation link" });

        var result = await _profiles.ConfirmEmailAsync(query.UserId, query.Token, cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, new { error = result.Error });
        return Ok(new { message = "Email confirmed" });
    }
}
