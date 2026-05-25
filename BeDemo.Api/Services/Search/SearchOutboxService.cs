using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Search;

/// <summary>Idempotent outbox enqueue for incremental search indexing (§6.1).</summary>
public sealed class SearchOutboxService : ISearchOutboxService
{
	private readonly ApplicationDbContext _db;
	private readonly IOptions<SearchOptions> _options;

	public SearchOutboxService(ApplicationDbContext db, IOptions<SearchOptions> options)
	{
		_db = db;
		_options = options;
	}

	/// <inheritdoc />
	public async Task EnqueueIndexAsync(string documentType, string entityId, CancellationToken cancellationToken = default)
	{
		if (!_options.Value.IsEnabled)
			return;

		StageIndex(documentType, entityId);
		await _db.SaveChangesAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task EnqueueDeleteAsync(string documentType, string entityId, CancellationToken cancellationToken = default)
	{
		if (!_options.Value.IsEnabled)
			return;

		StageDelete(documentType, entityId);
		await _db.SaveChangesAsync(cancellationToken);
	}

	/// <inheritdoc />
	public void StageIndex(string documentType, string entityId)
	{
		if (!_options.Value.IsEnabled)
			return;

		SearchOutboxStaging.StageIndex(_db, documentType, entityId);
	}

	/// <inheritdoc />
	public void StageDelete(string documentType, string entityId)
	{
		if (!_options.Value.IsEnabled)
			return;

		SearchOutboxStaging.StageDelete(_db, documentType, entityId);
	}
}
