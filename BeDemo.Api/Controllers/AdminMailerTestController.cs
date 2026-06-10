using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Security;
using BeDemo.Api.Services;
using ManyFaces.Mailer.V1;
using Microsoft.AspNetCore.Identity;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Operator-only mailer smoke (prompt §5.5): sends one <see cref="SendTemplatedEmailRequest"/> through <see cref="IMailerWorkerClient"/>
/// with <strong>no HTML composed in .NET</strong> — only template id + string params for the Java worker.
/// </summary>
[ApiController]
[Route("api/admin/mailer")]
// Backend-refactor X5/X6: the test-self operator gate is enforced by the ManageAllFaces policy at the class level
// instead of an in-body CanManageAllFaces check. The pilot-link action keeps its own [AllowAnonymous] (method-level
// wins over the class policy), so it stays publicly reachable. Same matrix for test-self (anonymous → 401,
// insufficient → 403, super-admin-in-admin-scope → allowed); pinned by AdminMailerTestController tests.
[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]
public sealed class AdminMailerTestController : ControllerBase
{
	private readonly IMailerWorkerClient _mailerWorker;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ILogger<AdminMailerTestController> _logger;

	public AdminMailerTestController(
		IMailerWorkerClient mailerWorker,
		UserManager<ApplicationUser> userManager,
		ILogger<AdminMailerTestController> logger)
	{
		_mailerWorker = mailerWorker;
		_userManager = userManager;
		_logger = logger;
	}

	/// <summary>
	/// Sends a pilot templated message to the signed-in operator's email (confirm-style template with a non-secret pilot URL).
	/// Requires <see cref="IAccessEvaluator.CanManageAllFaces"/> and a configured mail worker (<c>Mail:Enabled</c>).
	/// </summary>
	[HttpPost("test-self")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> TestSelf(CancellationToken cancellationToken)
	{
		// Authorization enforced by the ManageAllFaces policy on the controller. This guard only ensures a
		// NameIdentifier claim exists for the operator-self email lookup below — it is not an authz gate.
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrEmpty(userId))
		{
			return Unauthorized();
		}

		var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
		if (string.IsNullOrEmpty(user?.Email))
		{
			return BadRequest("Account has no email address.");
		}

		// Face-scoped routing: browsers hit /{face}/api/... — keep the pilot link consistent with portal/admin entry URLs.
		var facePrefix = "/" + Routing.ConvertToKebabCase("admin").Trim('/');
		var pilotUrl = $"{Request.Scheme}://{Request.Host.Value}{facePrefix}/api/admin/mailer/pilot-link";

		var display = string.IsNullOrWhiteSpace(user.UserName) ? user.Email!.Split('@')[0] : user.UserName!;
		var locale = System.Globalization.CultureInfo.CurrentUICulture.Name;
		if (string.IsNullOrWhiteSpace(locale))
		{
			locale = "en";
		}

		var request = new SendTemplatedEmailRequest();
		request.To.Add(user.Email!);
		request.TemplateId = MailTemplateIds.IdentityEmailConfirm;
		request.Locale = locale;
		request.Params["action_link"] = pilotUrl;
		request.Params["user_name"] = display;

		var response = await _mailerWorker.SendTemplatedEmailAsync(request, cancellationToken).ConfigureAwait(false);
		if (response is null)
		{
			return BadRequest("Mail worker is disabled or misconfigured (Mail:Enabled / Mail:WorkerGrpcUrl).");
		}

		_logger.LogInformation(
			"Admin mailer self-test sent template={Template} worker_correlation={Correlation}",
			request.TemplateId,
			response.CorrelationId);

		return Ok(new { response.CorrelationId, response.SmtpMessageId });
	}

	/// <summary>
	/// Lightweight GET target referenced from pilot mail (no secrets in query). Anonymous so an email client can open it without JWT.
	/// </summary>
	[HttpGet("pilot-link")]
	[AllowAnonymous]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public IActionResult PilotLink() => Ok("Mailer pilot link OK.");
}
