using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BeDemo.Api.Tests.Performance;

/// <summary>Captures the most recent EF SQL command text for tag/assertion tests.</summary>
internal sealed class SqlCommandTextCaptureInterceptor : DbCommandInterceptor
{
	public string? LastCommandText { get; private set; }

	public IReadOnlyList<string> CommandTexts => _texts;

	private readonly List<string> _texts = [];

	public void Reset()
	{
		LastCommandText = null;
		_texts.Clear();
	}

	private void Capture(DbCommand command)
	{
		LastCommandText = command.CommandText;
		_texts.Add(command.CommandText);
	}

	public override InterceptionResult<DbDataReader> ReaderExecuting(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<DbDataReader> result)
	{
		Capture(command);
		return base.ReaderExecuting(command, eventData, result);
	}

	public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<DbDataReader> result,
		CancellationToken cancellationToken = default)
	{
		Capture(command);
		return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
	}
}
