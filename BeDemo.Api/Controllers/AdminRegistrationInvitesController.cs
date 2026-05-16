using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Operator-managed registration invites (stance B). Requires <see cref="IAccessEvaluator.CanManageAllFaces"/>.
/// </summary>
[ApiController]
[Route("api/admin/registration-invites")]
[Authorize]
public sealed class AdminRegistrationInvitesController : ControllerBase
{
    private readonly IAccessEvaluator _access;
    private readonly IRegistrationInviteService _invites;

    public AdminRegistrationInvitesController(IAccessEvaluator access, IRegistrationInviteService invites)
    {
        _access = access;
        _invites = invites;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        if (!_access.CanManageAllFaces(User))
        {
            return Forbid();
        }

        var items = await _invites.ListAdminInvitesAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCreateRegistrationInviteDto dto, CancellationToken cancellationToken)
    {
        if (!_access.CanManageAllFaces(User))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var item = await _invites.CreateAdminInviteAsync(dto, userId, cancellationToken).ConfigureAwait(false);
        return Ok(item);
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        if (!_access.CanManageAllFaces(User))
        {
            return Forbid();
        }

        var ok = await _invites.RevokeAdminInviteAsync(id, cancellationToken).ConfigureAwait(false);
        return ok ? Ok() : NotFound();
    }
}
