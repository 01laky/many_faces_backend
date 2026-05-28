using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BeDemo.Api.Tests.Performance;

/// <summary>Counts EF database commands for performance edge-case assertions.</summary>
internal sealed class DbCommandCountInterceptor : DbCommandInterceptor
{
	public int CommandCount { get; private set; }

	public void Reset() => CommandCount = 0;

	public override InterceptionResult<DbDataReader> ReaderExecuting(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<DbDataReader> result)
	{
		CommandCount++;
		return base.ReaderExecuting(command, eventData, result);
	}

	public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<DbDataReader> result,
		CancellationToken cancellationToken = default)
	{
		CommandCount++;
		return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
	}

	public override InterceptionResult<int> NonQueryExecuting(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<int> result)
	{
		CommandCount++;
		return base.NonQueryExecuting(command, eventData, result);
	}

	public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		CommandCount++;
		return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
	}

	public override InterceptionResult<object> ScalarExecuting(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<object> result)
	{
		CommandCount++;
		return base.ScalarExecuting(command, eventData, result);
	}

	public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<object> result,
		CancellationToken cancellationToken = default)
	{
		CommandCount++;
		return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
	}
}
