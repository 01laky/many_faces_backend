using System.Security.Cryptography;
using System.Text;
using BeDemo.Api.Configuration;
using BeDemo.Api.Models.DTOs.Search;
using Grpc.Core;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Caching.Memory;
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
	private readonly SearchHitBatchFilter _batchFilter;
	private readonly IOptions<SearchOptions> _options;
	private readonly IOptions<PerformanceOptions> _performance;
	private readonly IMemoryCache _cache;
	private readonly IFaceScopeContext _faceScope;
	private readonly ILogger<AdminSearchAutocompleteService> _logger;

	public AdminSearchAutocompleteService(
		ISearchQueryGateway gateway,
		SearchHitBatchFilter batchFilter,
		IOptions<SearchOptions> options,
		IOptions<PerformanceOptions> performance,
		IMemoryCache cache,
		IFaceScopeContext faceScope,
		ILogger<AdminSearchAutocompleteService> logger)
	{
		_gateway = gateway;
		_batchFilter = batchFilter;
		_options = options;
		_performance = performance;
		_cache = cache;
		_faceScope = faceScope;
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

		var cacheKey = BuildCacheKey(query, offset, pageSize, documentTypes);
		if (_cache.TryGetValue(cacheKey, out AdminSearchAutocompleteResponse? cached) && cached is not null)
			return cached;

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

				var visibleFromPage = await _batchFilter.FilterVisibleAsync(response.Hits, cancellationToken);
				foreach (var hit in visibleFromPage)
				{
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
				.OrderByDescending(h => h.Score)
				.ThenBy(h => SearchDocumentTypes.SortOrder(h.EntityType))
				.ThenBy(h => h.Title, StringComparer.OrdinalIgnoreCase)
				.Take(pageSize)
				.ToList();

			var durationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
			SearchObservability.LogAutocompleteRequest(_logger, query, offset, pageSize, durationMs, visibleHits.Count);

			var result = new AdminSearchAutocompleteResponse
			{
				Query = query,
				Offset = offset,
				PageSize = pageSize,
				Hits = visibleHits,
				HasMore = workerHasMore,
				NextOffset = workerHasMore ? workerNextOffset : offset + visibleHits.Count,
				SearchAvailable = true,
			};

			var ttl = TimeSpan.FromSeconds(Math.Max(1, _performance.Value.AdminSearchAutocompleteCacheSeconds));
			_cache.Set(cacheKey, result, ttl);
			return result;
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

	private string BuildCacheKey(string query, int offset, int pageSize, IReadOnlyList<string>? documentTypes)
	{
		var types = documentTypes is { Count: > 0 }
			? string.Join(',', documentTypes.OrderBy(t => t, StringComparer.Ordinal))
			: "*";
		var faceId = _faceScope.IsAvailable ? _faceScope.FaceId : 0;
		var admin = _faceScope.IsAdminFaceScope;
		var payload = $"{query.Trim().ToLowerInvariant()}|{offset}|{pageSize}|{types}";
		var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
		return $"admin-ac:{faceId}:{admin}:{hash}";
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
