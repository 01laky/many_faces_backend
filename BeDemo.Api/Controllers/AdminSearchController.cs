using BeDemo.Api.Models.DTOs.Search;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeDemo.Api.Controllers;

/// <summary>Super-admin global search autocomplete on the admin face prefix (§3.2).</summary>
[ApiController]
[Route("api/search")]
[Authorize]
public sealed class AdminSearchController : ControllerBase
{
    private readonly IAccessEvaluator _access;
    private readonly IAdminSearchAutocompleteService _autocomplete;
    private readonly ILogger<AdminSearchController> _logger;

    public AdminSearchController(
        IAccessEvaluator access,
        IAdminSearchAutocompleteService autocomplete,
        ILogger<AdminSearchController> logger)
    {
        _access = access;
        _autocomplete = autocomplete;
        _logger = logger;
    }

    /// <summary>
    /// Paginated autocomplete for admin header global search.
    /// Requires admin face scope and <see cref="IAccessEvaluator.CanManageAllFaces"/> (SUPER_ADMIN).
    /// </summary>
    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(AdminSearchAutocompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminSearchAutocompleteResponse>> Autocomplete(
        [FromQuery] string? q,
        [FromQuery] int offset = 0,
        [FromQuery] int pageSize = AdminSearchAutocompleteService.DefaultPageSize,
        [FromQuery] string? types = null,
        CancellationToken cancellationToken = default)
    {
        if (!_access.CanManageAllFaces(User))
            return Forbid();

        if (offset < 0)
            return BadRequest(new { error = "offset must be a non-negative integer." });

        if (pageSize <= 0)
            return BadRequest(new { error = "pageSize must be a positive integer." });

        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "q is required." });

        pageSize = Math.Min(pageSize, AdminSearchAutocompleteService.MaxPageSize);

        IReadOnlyList<string>? documentTypes = null;
        if (!string.IsNullOrWhiteSpace(types))
        {
            documentTypes = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => SearchDocumentTypes.All.Contains(t))
                .ToList();
        }

        var trimmed = q.Trim();
        if (trimmed.Length == 1)
        {
            return Ok(new AdminSearchAutocompleteResponse
            {
                Query = trimmed,
                Offset = offset,
                PageSize = pageSize,
                Hits = [],
                HasMore = false,
                NextOffset = offset,
                SearchAvailable = true,
            });
        }

        var result = await _autocomplete.SearchAsync(trimmed, offset, pageSize, documentTypes, cancellationToken);
        if (!result.SearchAvailable)
        {
            _logger.LogDebug("Admin search autocomplete degraded: {Message}", result.Message);
        }

        return Ok(result);
    }
}
