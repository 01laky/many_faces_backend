using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Search;

/// <summary>
/// Stages search outbox rows in the same transaction as entity mutations (§6.1 incremental indexing).
/// </summary>
public sealed class SearchOutboxSaveChangesInterceptor : SaveChangesInterceptor
{
	private readonly IOptionsMonitor<SearchOptions> _options;

	public SearchOutboxSaveChangesInterceptor(IOptionsMonitor<SearchOptions> options) => _options = options;

	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		Process(eventData.Context);
		return base.SavingChanges(eventData, result);
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		Process(eventData.Context);
		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	private void Process(DbContext? context)
	{
		if (context is not ApplicationDbContext db || !_options.CurrentValue.IsEnabled)
			return;

		foreach (var entry in db.ChangeTracker.Entries().ToList())
		{
			if (entry.Entity is SearchOutboxEntry)
				continue;

			if (!SearchOutboxEntityMapper.TryMap(entry, out var documentType, out var entityId))
				continue;

			switch (entry.State)
			{
				case EntityState.Added:
				case EntityState.Modified:
					if (SearchOutboxEntityMapper.ShouldIndex(entry))
						SearchOutboxStaging.StageIndex(db, documentType, entityId);
					else
						SearchOutboxStaging.StageDelete(db, documentType, entityId);
					break;
				case EntityState.Deleted:
					SearchOutboxStaging.StageDelete(db, documentType, entityId);
					break;
			}
		}
	}
}
