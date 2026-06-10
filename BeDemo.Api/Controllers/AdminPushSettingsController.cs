using System.Security.Claims;
using BeDemo.Api.Models.DTOs.Admin;
using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Security;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorPush;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Controllers;

/// <summary>Operator push configuration (platform + Firebase FCM) for admin Settings.</summary>
[ApiController]
[Route("api/admin/push")]
// Backend-refactor X5/X6: operator gate (admin face scope + global SUPER_ADMIN) enforced declaratively by the
// ManageAllFaces policy instead of a per-action CanManageAllFaces check. Same matrix (anonymous → 401, insufficient
// → 403, super-admin-in-admin-scope → allowed); pinned by AdminPushSettingsController + PlatformSuperAdminAccessEdge.
[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]
public sealed class AdminPushSettingsController : ControllerBase
{
	private readonly IOperatorPushSettingsProvider _settings;
	private readonly AdminPushSettingsApplyService _apply;
	private readonly IPushWorkerClient _pushWorker;
	private readonly IOptions<PushOptions> _envPushOptions;
	private readonly IValidator<UpdateAdminPushSettingsRequest> _validator;
	private readonly ILogger<AdminPushSettingsController> _logger;

	public AdminPushSettingsController(
		IOperatorPushSettingsProvider settings,
		AdminPushSettingsApplyService apply,
		IPushWorkerClient pushWorker,
		IOptions<PushOptions> envPushOptions,
		IValidator<UpdateAdminPushSettingsRequest> validator,
		ILogger<AdminPushSettingsController> logger)
	{
		_settings = settings;
		_apply = apply;
		_pushWorker = pushWorker;
		_envPushOptions = envPushOptions;
		_validator = validator;
		_logger = logger;
	}

	[HttpGet("settings")]
	[ProducesResponseType(typeof(AdminPushSettingsDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<AdminPushSettingsDto>> GetSettings(CancellationToken cancellationToken)
	{
		var values = await _settings.GetAsync(cancellationToken);
		return Ok(_settings.ToDto(values, _envPushOptions.Value));
	}

	[HttpPut("settings")]
	[ProducesResponseType(typeof(AdminPushSettingsDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<AdminPushSettingsDto>> UpdateSettings(
		[FromBody] UpdateAdminPushSettingsRequest request,
		CancellationToken cancellationToken)
	{
		var validation = await _validator.ValidateAsync(request, cancellationToken);
		if (!validation.IsValid)
			return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		var current = await _settings.GetAsync(cancellationToken);
		var merged = _apply.Merge(current, request, userId);

		if (merged.Enabled && !merged.IsPushComplete)
		{
			return BadRequest("Push settings incomplete: worker URL, Firebase credentials, and default loc keys are required when enabled.");
		}

		var saved = await _settings.SetAsync(merged, cancellationToken);

		_logger.LogInformation(
			"Operator push settings updated by {UserId}; enabled={Enabled} effective={Status}",
			userId,
			saved.Enabled,
			saved.EffectiveStatus);

		return Ok(_settings.ToDto(saved, _envPushOptions.Value));
	}

	[HttpPost("settings/test-fcm")]
	[ProducesResponseType(typeof(AdminPushTestFcmResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<AdminPushTestFcmResultDto>> TestFcm(
		[FromBody] TestAdminPushFcmRequest? request,
		CancellationToken cancellationToken)
	{
		var values = await _settings.GetAsync(cancellationToken);
		var previewJson = request?.Firebase?.ServiceAccountJson;
		var jsonToProbe = !string.IsNullOrWhiteSpace(previewJson)
			? previewJson
			: values.FirebaseServiceAccountJsonPlaintext;

		if (string.IsNullOrWhiteSpace(jsonToProbe))
		{
			return Ok(new AdminPushTestFcmResultDto
			{
				FcmReachable = false,
				Message = "Firebase credentials not configured.",
			});
		}

		if (!FirebaseServiceAccountValidator.TryValidate(jsonToProbe, out var projectId, out var error))
		{
			return Ok(new AdminPushTestFcmResultDto
			{
				FcmReachable = false,
				Message = error,
			});
		}

		var probeValues = values with { FirebaseServiceAccountJsonPlaintext = jsonToProbe, FirebaseProjectId = projectId };
		var result = await _pushWorker.TestFcmCredentialsAsync(probeValues, cancellationToken);
		return Ok(new AdminPushTestFcmResultDto
		{
			FcmReachable = result?.Valid ?? false,
			ProjectId = result?.ProjectId ?? projectId,
			Message = result?.Detail,
		});
	}
}
