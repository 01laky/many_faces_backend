using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorMail;
using BeDemo.Api.Services.OperatorPush;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Operator-only read-only infrastructure probes for admin Settings (mail/push worker config flags).
/// </summary>
[ApiController]
[Route("api/admin/infra")]
[Authorize]
public sealed class AdminInfraController : ControllerBase
{
	private readonly IAccessEvaluator _access;
	private readonly ApplicationDbContext _db;
	private readonly IOperatorMailSettingsProvider _mailSettings;
	private readonly IOperatorPushSettingsProvider _pushSettings;

	public AdminInfraController(
		IAccessEvaluator access,
		ApplicationDbContext db,
		IOperatorMailSettingsProvider mailSettings,
		IOperatorPushSettingsProvider pushSettings)
	{
		_access = access;
		_db = db;
		_mailSettings = mailSettings;
		_pushSettings = pushSettings;
	}

	/// <summary>
	/// Returns whether optional mail/push workers are configured and how many push devices the caller has registered.
	/// Does not expose gRPC URLs, auth tokens, or certificate paths.
	/// </summary>
	[HttpGet("worker-config")]
	[ProducesResponseType(typeof(AdminInfraWorkerConfigDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<AdminInfraWorkerConfigDto>> GetWorkerConfig(CancellationToken cancellationToken)
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

		var deviceCount = await _db.UserPushDevices
			.AsNoTracking()
			.CountAsync(d => d.UserId == userId, cancellationToken);

		var mailValues = await _mailSettings.GetAsync(cancellationToken);
		var pushValues = await _pushSettings.GetAsync(cancellationToken);

		return Ok(new AdminInfraWorkerConfigDto
		{
			Mail = new AdminInfraMailWorkerConfigDto
			{
				Configured = mailValues.EffectiveStatus == OperatorMailEffectiveStatuses.Configured,
				EffectiveStatus = mailValues.EffectiveStatus,
			},
			Push = new AdminInfraPushWorkerConfigDto
			{
				Configured = pushValues.EffectiveStatus == OperatorPushEffectiveStatuses.Configured,
				EffectiveStatus = pushValues.EffectiveStatus,
				RegisteredDeviceCount = deviceCount,
			},
		});
	}
}
