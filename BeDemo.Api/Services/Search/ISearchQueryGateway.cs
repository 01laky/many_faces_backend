using ManyFaces.Search.V1;

namespace BeDemo.Api.Services.Search;

/// <summary>gRPC gateway to the Go search-worker (index, delete, bulk, list ids, autocomplete).</summary>
public interface ISearchQueryGateway
{
	/// <summary>True when search is enabled and the gRPC client was constructed.</summary>
	bool IsAvailable { get; }

	Task<AutocompleteResponse?> AutocompleteAsync(AutocompleteRequest request, CancellationToken cancellationToken = default);

	Task<IndexDocumentResponse?> IndexDocumentAsync(IndexDocumentRequest request, CancellationToken cancellationToken = default);

	Task<DeleteDocumentResponse?> DeleteDocumentAsync(DeleteDocumentRequest request, CancellationToken cancellationToken = default);

	Task<BulkIndexDocumentsResponse?> BulkIndexDocumentsAsync(BulkIndexDocumentsRequest request, CancellationToken cancellationToken = default);

	Task<ListDocumentIdsResponse?> ListDocumentIdsAsync(ListDocumentIdsRequest request, CancellationToken cancellationToken = default);
}
