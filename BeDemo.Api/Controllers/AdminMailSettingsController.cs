using System.Security.Claims;
using BeDemo.Api.Models.DTOs.Admin;
using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Security;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorMail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeDemo.Api.Controllers;

/// <summary>Operator mail configuration (platform + SMTP) for admin Settings.</summary>
[ApiController]
[Route("api/admin/mail")]
// Backend-refactor X5/X6: operator gate (admin face scope + global SUPER_ADMIN) enforced declaratively by the
// ManageAllFaces policy instead of a per-action CanManageAllFaces check. Same matrix (anonymous → 401, insufficient
// → 403, super-admin-in-admin-scope → allowed); pinned by AdminMailSettingsController + PlatformSuperAdminAccessEdge.
[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]
public sealed class AdminMailSettingsController : ControllerBase
{
	private readonly IOperatorMailSettingsProvider _settings;
	private readonly AdminMailSettingsApplyService _apply;
	private readonly IMailerWorkerClient _mailerWorker;
	private readonly ILogger<AdminMailSettingsController> _logger;

	public AdminMailSettingsController(
		IOperatorMailSettingsProvider settings,
		AdminMailSettingsApplyService apply,
		IMailerWorkerClient mailerWorker,
		ILogger<AdminMailSettingsController> logger)
	{
		_settings = settings;
		_apply = apply;
		_mailerWorker = mailerWorker;
		_logger = logger;
	}

	[HttpGet("settings")]
	[ProducesResponseType(typeof(AdminMailSettingsDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<AdminMailSettingsDto>> GetSettings(CancellationToken cancellationToken)
	{
		var values = await _settings.GetAsync(cancellationToken);
		return Ok(_settings.ToDto(values));
	}

	[HttpPut("settings")]
	[ProducesResponseType(typeof(AdminMailSettingsDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<AdminMailSettingsDto>> UpdateSettings(
		[FromBody] UpdateAdminMailSettingsRequest request,
		CancellationToken cancellationToken)
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		var current = await _settings.GetAsync(cancellationToken);
		var merged = _apply.Merge(current, request, userId);
		var saved = await _settings.SetAsync(merged, cancellationToken);

		_logger.LogInformation(
			"Operator mail settings updated by {UserId}; enabled={Enabled} effective={Status}",
			userId,
			saved.Enabled,
			saved.EffectiveStatus);

		return Ok(_settings.ToDto(saved));
	}

	[HttpPost("settings/test-smtp")]
	[ProducesResponseType(typeof(AdminMailTestSmtpResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<AdminMailTestSmtpResultDto>> TestSmtp(CancellationToken cancellationToken)
	{
		var values = await _settings.GetAsync(cancellationToken);
		if (!values.IsSmtpComplete)
		{
			return Ok(new AdminMailTestSmtpResultDto
			{
				SmtpReachable = false,
				Message = "SMTP settings incomplete.",
			});
		}

		var result = await _mailerWorker.TestSmtpConnectionAsync(values, cancellationToken);
		return Ok(new AdminMailTestSmtpResultDto
		{
			SmtpReachable = result?.Reachable ?? false,
			Message = result?.Detail,
		});
	}
}
