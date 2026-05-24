using BeDemo.Api.Services.OperatorAi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BeDemo.Api.Controllers;

/// <summary>Public read-only probe for portal UI to disable AI chat send when the global switch is off.</summary>
[ApiController]
[AllowAnonymous]
public sealed class AiAvailabilityController : ControllerBase
{
    private readonly IOperatorAiSystemSettingsProvider _settings;

    public AiAvailabilityController(IOperatorAiSystemSettingsProvider settings) => _settings = settings;

    [HttpGet("~/api/ai/enabled")]
    [EnableRateLimiting("ai-availability-read")]
    public async Task<ActionResult<AiEnabledResponse>> GetEnabled(CancellationToken cancellationToken)
    {
        var enabled = await _settings.IsAiEnabledAsync(cancellationToken);
        return Ok(new AiEnabledResponse { Enabled = enabled });
    }
}

public sealed class AiEnabledResponse
{
    public bool Enabled { get; set; }
}
