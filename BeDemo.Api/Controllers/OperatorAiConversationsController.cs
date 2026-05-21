using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Models.Requests.OperatorAi;
using BeDemo.Api.Services;
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
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<OperatorAiConversationsController> _logger;

    public OperatorAiConversationsController(
        IAccessEvaluator access,
        IOperatorAiConversationService operatorAi,
        IAiGrpcService aiGrpc,
        IAiWorkerHostProfileService workerHost,
        IHubContext<ChatHub> hub,
        ILogger<OperatorAiConversationsController> logger)
    {
        _access = access;
        _operatorAi = operatorAi;
        _aiGrpc = aiGrpc;
        _workerHost = workerHost;
        _hub = hub;
        _logger = logger;
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

        await _workerHost.RefreshFromWorkerAsync(cancellationToken);
        return Ok(await _workerHost.GetOperatorViewAsync(cancellationToken));
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
