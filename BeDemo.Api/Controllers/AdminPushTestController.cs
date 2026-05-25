using System.Security.Claims;
using BeDemo.Api.Data;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorPush;
using ManyFaces.Push.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Operator-only push smoke tests (never expose arbitrary send-to-user from this controller in early phases).
/// </summary>
[ApiController]
[Route("api/admin/push")]
[Authorize]
public sealed class AdminPushTestController : ControllerBase
{
	private readonly ApplicationDbContext _db;
	private readonly IAccessEvaluator _access;
	private readonly IPushWorkerClient _pushWorker;
	private readonly IOperatorPushSettingsProvider _pushSettings;
	private readonly ILogger<AdminPushTestController> _logger;

	public AdminPushTestController(
		ApplicationDbContext db,
		IAccessEvaluator access,
		IPushWorkerClient pushWorker,
		IOperatorPushSettingsProvider pushSettings,
		ILogger<AdminPushTestController> logger)
	{
		_db = db;
		_access = access;
		_pushWorker = pushWorker;
		_pushSettings = pushSettings;
		_logger = logger;
	}

	/// <summary>
	/// Sends a minimal localized test notification to every device token registered for the calling operator account.
	/// Requires <see cref="IAccessEvaluator.CanManageAllFaces"/> and configured operator push settings.
	/// </summary>
	[HttpPost("test-self")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> TestSelf(CancellationToken cancellationToken)
	{
		if (!_access.CanManageAllFaces(User))
		{
			return Forbid();
		}

		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrEmpty(userId))
		{
			return Unauthorized();
		}

		var settings = await _pushSettings.GetAsync(cancellationToken);
		if (!settings.IsSendAllowed)
		{
			return BadRequest("Push worker is disabled or misconfigured. Save push settings in Infrastructure first.");
		}

		var devices = await _db.UserPushDevices.Where(d => d.UserId == userId).ToListAsync(cancellationToken);
		if (devices.Count == 0)
		{
			return BadRequest("No push devices registered for this account. Call POST /api/me/push-token from the mobile app first.");
		}

		var tokens = devices.Select(d => d.RegistrationToken).Distinct().ToList();
		var request = new SendPushRequest
		{
			TitleLocKey = settings.DefaultTitleLocKey,
			BodyLocKey = settings.DefaultBodyLocKey,
			AndroidChannelId = settings.DefaultAndroidChannelId ?? string.Empty,
			TtlSeconds = 300,
		};
		request.RegistrationTokens.AddRange(tokens);
		request.Data["route"] = "push-test";

		var response = await _pushWorker.SendPushAsync(request, cancellationToken);
		if (response is null)
		{
			return BadRequest("Push worker is disabled or misconfigured.");
		}

		var invalidTokens = new HashSet<string>(StringComparer.Ordinal);
		for (var i = 0; i < response.Results.Count && i < tokens.Count; i++)
		{
			var r = response.Results[i];
			if (r.PermanentInvalid)
			{
				invalidTokens.Add(tokens[i]);
			}
		}

		if (invalidTokens.Count > 0)
		{
			var toRemove = await _db.UserPushDevices.Where(d => invalidTokens.Contains(d.RegistrationToken)).ToListAsync(cancellationToken);
			_db.UserPushDevices.RemoveRange(toRemove);
			await _db.SaveChangesAsync(cancellationToken);
		}

		_logger.LogInformation(
			"Admin push self-test completed: sent={Sent}, failed={Failed}, pruned_invalid_rows={Pruned}",
			response.Sent,
			response.Failed,
			invalidTokens.Count);

		return Ok(new
		{
			response.Sent,
			response.Failed,
			prunedInvalidTokens = invalidTokens.Count,
		});
	}
}
