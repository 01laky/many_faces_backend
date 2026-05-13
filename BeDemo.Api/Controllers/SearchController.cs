using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Optional search infrastructure (gRPC health probe to the Go search-worker; Elasticsearch HTTP stays inside many_faces_elastic).
/// </summary>
[ApiController]
[Route("api/search")]
[Authorize]
public sealed class SearchController : ControllerBase
{
    private readonly ISearchWorkerProbe _probe;
    private readonly ILogger<SearchController> _logger;

    public SearchController(ISearchWorkerProbe probe, ILogger<SearchController> logger)
    {
        _probe = probe;
        _logger = logger;
    }

    /// <summary>
    /// Returns whether search is configured and whether the Go worker reports Elasticsearch reachability (via gRPC Ping).
    /// Anonymous callers may use the <b>public</b> face URL prefix (same pattern as <c>GET /api/Stats/public</c>).
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<ActionResult<SearchHealthDto>> GetHealth(CancellationToken cancellationToken)
    {
        var result = await _probe.GetHealthAsync(cancellationToken);
        if (result.Configured && !result.Reachable)
        {
            _logger.LogWarning("Search health: worker or Elasticsearch unreachable: {Message}", result.Message);
        }

        return Ok(result);
    }
}
