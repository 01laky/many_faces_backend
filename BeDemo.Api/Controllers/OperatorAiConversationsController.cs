using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Models.Requests.OperatorAi;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Validation.OperatorAi;

namespace BeDemo.Api.Controllers;

/// <summary>Shared operator AI support inbox — CRUD threads and paginated messages (admin face scope).</summary>
[ApiController]
[Route("api/operator-ai/conversations")]
[Authorize]
public sealed class OperatorAiConversationsController : ControllerBase
{
    private readonly IAccessEvaluator _access;
    private readonly IOperatorAiConversationService _operatorAi;
    private readonly IAiGrpcService _aiGrpc;
    private readonly IAiWorkerHostProfileService _workerHost;
    private readonly IOperatorAiLiveStatsCacheSettingsProvider _liveStatsCacheSettings;
    private readonly IOperatorAiPublicStatsSettingsProvider _publicStatsSettings;
    private readonly IOperatorAiSystemSettingsProvider _systemSettings;
    private readonly IOperatorAiEnableService _enableService;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<OperatorAiConversationsController> _logger;

    public OperatorAiConversationsController(
        IAccessEvaluator access,
        IOperatorAiConversationService operatorAi,
        IAiGrpcService aiGrpc,
        IAiWorkerHostProfileService workerHost,
        IOperatorAiLiveStatsCacheSettingsProvider liveStatsCacheSettings,
        IOperatorAiPublicStatsSettingsProvider publicStatsSettings,
        IOperatorAiSystemSettingsProvider systemSettings,
        IOperatorAiEnableService enableService,
        IHubContext<ChatHub> hub,
        ILogger<OperatorAiConversationsController> logger)
    {
        _access = access;
        _operatorAi = operatorAi;
        _aiGrpc = aiGrpc;
        _workerHost = workerHost;
        _liveStatsCacheSettings = liveStatsCacheSettings;
        _publicStatsSettings = publicStatsSettings;
        _systemSettings = systemSettings;
        _enableService = enableService;
        _hub = hub;
        _logger = logger;
    }

    [HttpGet("~/api/operator-ai/system-settings")]
    public async Task<ActionResult<OperatorAiSystemSettingsDto>> GetSystemSettings(CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        var values = await _systemSettings.GetAsync(cancellationToken);
        return Ok(_systemSettings.ToDto(values));
    }

    [HttpPut("~/api/operator-ai/system-settings")]
    public async Task<ActionResult<OperatorAiSystemSettingsDto>> UpdateSystemSettings(
        [FromBody] UpdateOperatorAiSystemSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        var validation = new UpdateOperatorAiSystemSettingsValidator().Validate(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (request.AiEnabled)
        {
            var outcome = await _enableService.EnableAsync(userId, cancellationToken);
            if (!outcome.Success)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { error = "AI could not be enabled.", errorCode = outcome.ErrorCode });
            }

            return Ok(outcome.Settings);
        }

        return Ok(await _enableService.DisableAsync(userId, cancellationToken));
    }

    [HttpGet("~/api/operator-ai/model-status")]
    public async Task<ActionResult<OperatorAiModelStatusDto>> GetModelStatus(CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        var status = await _aiGrpc.GetModelStatusAsync(cancellationToken);
        return Ok(new OperatorAiModelStatusDto
        {
            Ready = status.Ready,
            Loading = status.Loading,
            Unavailable = status.Unavailable,
            ModelName = status.ModelName,
        });
    }

    [HttpGet("~/api/operator-ai/worker-host")]
    public async Task<ActionResult<OperatorAiWorkerHostDto>> GetWorkerHost(CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        return Ok(await _workerHost.GetOperatorViewAsync(cancellationToken));
    }

    [HttpPost("~/api/operator-ai/worker-host/refresh")]
    public async Task<ActionResult<OperatorAiWorkerHostDto>> RefreshWorkerHost(CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        if (!await _systemSettings.IsAiEnabledAsync(cancellationToken))
            return Conflict(new { error = "Enable AI support in Settings before refreshing the worker host." });

        await _workerHost.RefreshFromWorkerAsync(cancellationToken);
        return Ok(await _workerHost.GetOperatorViewAsync(cancellationToken));
    }

    [HttpGet("~/api/operator-ai/live-stats-cache")]
    public async Task<ActionResult<OperatorAiLiveStatsCacheSettingsDto>> GetLiveStatsCacheSettings(
        CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        var ttlMs = await _liveStatsCacheSettings.GetTtlMillisecondsAsync(cancellationToken);
        return Ok(_liveStatsCacheSettings.ToDto(ttlMs));
    }

    [HttpPut("~/api/operator-ai/live-stats-cache")]
    public async Task<ActionResult<OperatorAiLiveStatsCacheSettingsDto>> UpdateLiveStatsCacheSettings(
        [FromBody] UpdateOperatorAiLiveStatsCacheSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        if (!await _systemSettings.IsAiEnabledAsync(cancellationToken))
            return Conflict(new { error = "Enable AI support in Settings before changing live stats cache TTL." });

        var validation = new UpdateOperatorAiLiveStatsCacheSettingsValidator().Validate(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var ttlMs = await _liveStatsCacheSettings.SetTtlMillisecondsAsync(
            request.TtlMilliseconds,
            userId,
            cancellationToken);
        return Ok(_liveStatsCacheSettings.ToDto(ttlMs));
    }

    [HttpGet("~/api/operator-ai/public-stats-settings")]
    public async Task<ActionResult<OperatorAiPublicStatsSettingsDto>> GetPublicStatsSettings(
        CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        var values = await _publicStatsSettings.GetAsync(cancellationToken);
        return Ok(_publicStatsSettings.ToDto(values));
    }

    [HttpPut("~/api/operator-ai/public-stats-settings")]
    public async Task<ActionResult<OperatorAiPublicStatsSettingsDto>> UpdatePublicStatsSettings(
        [FromBody] UpdateOperatorAiPublicStatsSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        if (!await _systemSettings.IsAiEnabledAsync(cancellationToken))
            return Conflict(new { error = "Enable AI support in Settings before changing public stats mode." });

        var validation = new UpdateOperatorAiPublicStatsSettingsValidator().Validate(request);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var values = await _publicStatsSettings.SetAsync(
            new OperatorAiPublicStatsSettingsValues(
                request.PublicStatsMode,
                request.LiveMaxParallelBundleCalls),
            userId,
            cancellationToken);
        return Ok(_publicStatsSettings.ToDto(values));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OperatorAiConversationListItemDto>>> List(
        [FromQuery] OperatorAiConversationsListQuery query,
        CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        return Ok(await _operatorAi.ListConversationsAsync(query.Limit, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OperatorAiConversationListItemDto>> Get(int id, CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        var item = await _operatorAi.GetConversationAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<OperatorAiConversationListItemDto>> Create(
        [FromBody] CreateOperatorAiConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        if (!await _systemSettings.IsAiEnabledAsync(cancellationToken))
            return Conflict(new { error = "Enable AI support in Settings before creating operator AI conversations." });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Authenticated user id missing.");
        var created = await _operatorAi.CreateConversationAsync(userId, request, cancellationToken);
        await _hub.Clients.Group(OperatorAiHubGroups.Operators)
            .SendAsync("OperatorAiConversationListChanged", created, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<OperatorAiConversationListItemDto>> Update(
        int id,
        [FromBody] UpdateOperatorAiConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        var updated = await _operatorAi.UpdateConversationAsync(id, request, cancellationToken);
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        if (!await _operatorAi.DeleteConversationAsync(id, cancellationToken))
            return NotFound();

        await _hub.Clients.Group(OperatorAiHubGroups.Operators)
            .SendAsync(
                "OperatorAiConversationDeleted",
                new OperatorAiConversationDeletedEventDto { ConversationId = id },
                cancellationToken);

        _logger.LogInformation("Operator AI conversation {ConversationId} deleted.", id);
        return NoContent();
    }

    [HttpGet("{id:int}/messages")]
    public async Task<ActionResult<OperatorAiMessagesPageDto>> GetMessages(
        int id,
        [FromQuery] OperatorAiMessagesQuery query,
        CancellationToken cancellationToken)
    {
        if (!RequireOperator())
            return Forbid();

        var conv = await _operatorAi.GetConversationAsync(id, cancellationToken);
        if (conv == null)
            return NotFound();

        return Ok(await _operatorAi.GetMessagesPageAsync(id, query, cancellationToken));
    }

    private bool RequireOperator()
    {
        if (_access.CanManageAllFaces(User))
            return true;

        _logger.LogDebug("Operator AI denied: user lacks CanManageAllFaces.");
        return false;
    }
}
