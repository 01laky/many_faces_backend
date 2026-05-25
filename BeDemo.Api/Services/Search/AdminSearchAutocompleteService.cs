using BeDemo.Api.Models.DTOs.Search;
using Grpc.Core;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Search;

public interface IAdminSearchAutocompleteService
{
    Task<AdminSearchAutocompleteResponse> SearchAsync(
        string query,
        int offset,
        int pageSize,
        IReadOnlyList<string>? documentTypes,
        CancellationToken cancellationToken);
}

/// <summary>Orchestrates worker autocomplete, ACL filter, XSS sanitization, and pagination (§3.2).</summary>
public sealed class AdminSearchAutocompleteService : IAdminSearchAutocompleteService
{
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 100;
    public const int MinQueryLength = 2;
    private const int MaxWorkerPages = 10;

    private readonly ISearchQueryGateway _gateway;
    private readonly SearchHitAclFilter _acl;
    private readonly IOptions<SearchOptions> _options;
    private readonly ILogger<AdminSearchAutocompleteService> _logger;

    public AdminSearchAutocompleteService(
        ISearchQueryGateway gateway,
        SearchHitAclFilter acl,
        IOptions<SearchOptions> options,
        ILogger<AdminSearchAutocompleteService> logger)
    {
        _gateway = gateway;
        _acl = acl;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AdminSearchAutocompleteResponse> SearchAsync(
        string query,
        int offset,
        int pageSize,
        IReadOnlyList<string>? documentTypes,
        CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        if (!_options.Value.IsEnabled || !_gateway.IsAvailable)
        {
            return DegradedResponse(query, offset, pageSize, "Search is disabled or not configured.");
        }

        if (query.Trim().Length < MinQueryLength)
        {
            return new AdminSearchAutocompleteResponse
            {
                Query = query,
                Offset = offset,
                PageSize = pageSize,
                Hits = [],
                HasMore = false,
                NextOffset = offset,
                SearchAvailable = true,
            };
        }

        try
        {
            var visibleHits = new List<AdminSearchAutocompleteHitDto>();
            var workerOffset = offset;
            var workerHasMore = false;
            var workerNextOffset = offset;
            var pagesFetched = 0;

            while (visibleHits.Count < pageSize && pagesFetched < MaxWorkerPages)
            {
                var request = new AutocompleteRequest
                {
                    Query = query,
                    PageSize = pageSize,
                    Offset = workerOffset,
                    CorrelationId = SearchWorkerGrpcGateway.NewCorrelationId(),
                };
                if (documentTypes is { Count: > 0 })
                    request.DocumentTypes.AddRange(documentTypes);

                var response = await _gateway.AutocompleteAsync(request, cancellationToken);
                if (response is null)
                    return DegradedResponse(query, offset, pageSize, "Search worker is unavailable.");

                foreach (var hit in response.Hits)
                {
                    if (!await _acl.IsVisibleAsync(hit, cancellationToken))
                        continue;

                    visibleHits.Add(SearchAutocompleteSanitizer.ToDto(hit));
                    if (visibleHits.Count >= pageSize)
                        break;
                }

                workerHasMore = response.HasMore;
                workerNextOffset = response.NextOffset;
                pagesFetched++;

                if (!response.HasMore || response.Hits.Count == 0)
                    break;

                if (visibleHits.Count >= pageSize)
                    break;

                workerOffset = response.NextOffset;
            }

            visibleHits = visibleHits
                .OrderBy(h => SearchDocumentTypes.SortOrder(h.EntityType))
                .ThenByDescending(h => h.Title, StringComparer.OrdinalIgnoreCase)
                .Take(pageSize)
                .ToList();

            var durationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
            SearchObservability.LogAutocompleteRequest(_logger, query, offset, pageSize, durationMs, visibleHits.Count);

            return new AdminSearchAutocompleteResponse
            {
                Query = query,
                Offset = offset,
                PageSize = pageSize,
                Hits = visibleHits,
                HasMore = workerHasMore,
                NextOffset = workerHasMore ? workerNextOffset : offset + visibleHits.Count,
                SearchAvailable = true,
            };
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Autocomplete gRPC failed: {Code}", ex.StatusCode);
            return DegradedResponse(query, offset, pageSize, "Search is temporarily unavailable.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Autocomplete failed");
            return DegradedResponse(query, offset, pageSize, "Search is temporarily unavailable.");
        }
    }

    private static AdminSearchAutocompleteResponse DegradedResponse(string query, int offset, int pageSize, string message) =>
        new()
        {
            Query = query,
            Offset = offset,
            PageSize = pageSize,
            Hits = [],
            HasMore = false,
            NextOffset = offset,
            SearchAvailable = false,
            Message = message,
        };
}
