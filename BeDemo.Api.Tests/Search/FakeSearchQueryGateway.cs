using ManyFaces.Search.V1;

namespace BeDemo.Api.Tests.Search;

/// <summary>In-memory fake for <see cref="BeDemo.Api.Services.Search.ISearchQueryGateway"/> used in search edge tests.</summary>
internal sealed class FakeSearchQueryGateway : BeDemo.Api.Services.Search.ISearchQueryGateway
{
	public bool IsAvailable { get; set; } = true;

	public int AutocompleteCallCount { get; private set; }

	public int IndexDocumentCallCount { get; private set; }

	public int DeleteDocumentCallCount { get; private set; }

	public int BulkIndexCallCount { get; private set; }

	public int ListDocumentIdsCallCount { get; private set; }

	public AutocompleteResponse? NextAutocompleteResponse { get; set; }

	public Exception? AutocompleteException { get; set; }

	public Func<AutocompleteRequest, AutocompleteResponse>? AutocompleteHandler { get; set; }

	public IndexDocumentResponse IndexDocumentResponse { get; set; } = new() { Success = true };

	public DeleteDocumentResponse DeleteDocumentResponse { get; set; } = new() { Success = true };

	public BulkIndexDocumentsResponse BulkIndexResponse { get; set; } = new() { IndexedCount = 1 };

	public ListDocumentIdsResponse ListDocumentIdsResponse { get; set; } = new();

	/// <summary>Artificial delay for deadline / concurrency edge tests (BE-RP32).</summary>
	public int BulkIndexDelayMs { get; set; }

	/// <summary>Peak concurrent in-flight bulk index calls observed.</summary>
	public int MaxObservedBulkConcurrency => Volatile.Read(ref _maxObservedBulkConcurrency);

	private int _bulkInFlight;
	private int _maxObservedBulkConcurrency;

	public int IndexFailCountBeforeSuccess { get; set; }

	public int DeleteFailCountBeforeSuccess { get; set; }

	private int _indexAttempts;
	private int _deleteAttempts;

	public void ResetIndexAttempts() => _indexAttempts = 0;

	public void ResetDeleteAttempts() => _deleteAttempts = 0;

	public Task<AutocompleteResponse?> AutocompleteAsync(AutocompleteRequest request, CancellationToken cancellationToken = default)
	{
		AutocompleteCallCount++;
		if (AutocompleteException is not null)
			throw AutocompleteException;

		if (AutocompleteHandler is not null)
			return Task.FromResult<AutocompleteResponse?>(AutocompleteHandler(request));

		return Task.FromResult(NextAutocompleteResponse);
	}

	public Task<IndexDocumentResponse?> IndexDocumentAsync(IndexDocumentRequest request, CancellationToken cancellationToken = default)
	{
		IndexDocumentCallCount++;
		_indexAttempts++;
		if (_indexAttempts <= IndexFailCountBeforeSuccess)
			return Task.FromResult<IndexDocumentResponse?>(new IndexDocumentResponse { Success = false, ErrorMessage = "fail" });
		return Task.FromResult<IndexDocumentResponse?>(IndexDocumentResponse);
	}

	public Task<DeleteDocumentResponse?> DeleteDocumentAsync(DeleteDocumentRequest request, CancellationToken cancellationToken = default)
	{
		DeleteDocumentCallCount++;
		_deleteAttempts++;
		if (_deleteAttempts <= DeleteFailCountBeforeSuccess)
			return Task.FromResult<DeleteDocumentResponse?>(new DeleteDocumentResponse { Success = false, ErrorMessage = "fail" });
		return Task.FromResult<DeleteDocumentResponse?>(DeleteDocumentResponse);
	}

	public async Task<BulkIndexDocumentsResponse?> BulkIndexDocumentsAsync(BulkIndexDocumentsRequest request, CancellationToken cancellationToken = default)
	{
		BulkIndexCallCount++;
		var inFlight = Interlocked.Increment(ref _bulkInFlight);
		while (true)
		{
			var currentMax = Volatile.Read(ref _maxObservedBulkConcurrency);
			if (inFlight <= currentMax || Interlocked.CompareExchange(ref _maxObservedBulkConcurrency, inFlight, currentMax) == currentMax)
				break;
		}

		try
		{
			if (BulkIndexDelayMs > 0)
				await Task.Delay(BulkIndexDelayMs, cancellationToken);
		}
		finally
		{
			Interlocked.Decrement(ref _bulkInFlight);
		}

		var response = BulkIndexResponse;
		if (response.IndexedCount == 0 || response.IndexedCount < request.Documents.Count)
		{
			response = new BulkIndexDocumentsResponse
			{
				IndexedCount = request.Documents.Count,
				FailedCount = response.FailedCount,
			};
			response.Errors.AddRange(BulkIndexResponse.Errors);
		}

		return response;
	}

	public Task<ListDocumentIdsResponse?> ListDocumentIdsAsync(ListDocumentIdsRequest request, CancellationToken cancellationToken = default)
	{
		ListDocumentIdsCallCount++;
		return Task.FromResult<ListDocumentIdsResponse?>(ListDocumentIdsResponse);
	}
}
