using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Authenticated mobile endpoints under <c>/api/me/*</c> (push token registration for FCM).
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MePushTokenController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<MePushTokenController> _logger;

    public MePushTokenController(ApplicationDbContext db, ILogger<MePushTokenController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Registers or refreshes the caller's FCM registration token (idempotent per installation when <see cref="RegisterPushTokenRequestDto.InstallationId"/> is sent).
    /// </summary>
    [HttpPost("push-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterPushToken([FromBody] RegisterPushTokenRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var platform = (dto.Platform ?? string.Empty).Trim().ToLowerInvariant();
        if (platform is not ("ios" or "android"))
        {
            return BadRequest("Platform must be 'ios' or 'android'.");
        }

        var token = (dto.RegistrationToken ?? string.Empty).Trim();
        if (token.Length < 10)
        {
            return BadRequest("RegistrationToken is required.");
        }

        var now = DateTime.UtcNow;

        // A registration token must not be shared across users — reclaim it if another row still holds it.
        var staleOwners = await _db.UserPushDevices.Where(d => d.RegistrationToken == token && d.UserId != userId).ToListAsync(cancellationToken);
        if (staleOwners.Count > 0)
        {
            _db.UserPushDevices.RemoveRange(staleOwners);
        }

        UserPushDevice? row = null;
        var installationId = string.IsNullOrWhiteSpace(dto.InstallationId) ? null : dto.InstallationId.Trim();
        if (installationId is not null)
        {
            row = await _db.UserPushDevices.FirstOrDefaultAsync(
                d => d.UserId == userId && d.InstallationId == installationId,
                cancellationToken);
        }

        row ??= await _db.UserPushDevices.FirstOrDefaultAsync(d => d.UserId == userId && d.RegistrationToken == token, cancellationToken);

        if (row is null)
        {
            row = new UserPushDevice
            {
                UserId = userId,
                Platform = platform,
                RegistrationToken = token,
                InstallationId = installationId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                LastSeenAtUtc = now,
            };
            _db.UserPushDevices.Add(row);
        }
        else
        {
            row.Platform = platform;
            row.RegistrationToken = token;
            row.InstallationId = installationId;
            row.UpdatedAtUtc = now;
            row.LastSeenAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Push token registered for user (platform {Platform}, installation set: {HasInstallation})", platform, installationId is not null);
        return NoContent();
    }

    /// <summary>
    /// Removes push device rows for the caller. When <paramref name="installationId"/> is set, only that installation is removed; otherwise all rows for the user are deleted (sign-out-all-devices style).
    /// </summary>
    [HttpDelete("push-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnregisterPushToken([FromQuery] string? installationId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var q = _db.UserPushDevices.Where(d => d.UserId == userId);
        if (!string.IsNullOrWhiteSpace(installationId))
        {
            var id = installationId.Trim();
            q = q.Where(d => d.InstallationId == id);
        }

        var rows = await q.ToListAsync(cancellationToken);
        if (rows.Count > 0)
        {
            _db.UserPushDevices.RemoveRange(rows);
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Push token(s) removed for user (count {Count}, scopedToInstallation: {Scoped})", rows.Count, !string.IsNullOrWhiteSpace(installationId));
        return NoContent();
    }
}
