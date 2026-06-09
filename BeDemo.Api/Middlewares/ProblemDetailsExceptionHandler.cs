using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BeDemo.Api.Middlewares;

/// <summary>
/// Global exception handler (backend-refactor X4) that turns an unhandled exception into a single, consistent
/// RFC 7807 <c>application/problem+json</c> 500 response instead of a bare/blank 500 or a leaked stack trace. It is
/// opt-in behind the <c>ErrorHandling:UseProblemDetails</c> flag (default off) — registered + wired only when enabled,
/// so existing error behaviour is unchanged until an operator turns it on (prompt §10: ship cross-cutting changes
/// behind a flag).
/// <para>
/// PII/leak discipline: the exception is logged server-side (with the correlation id) but the response body carries
/// only a generic title plus the <c>traceId</c>; the exception detail is included <em>only</em> in the Development
/// environment, never in Production/Staging.
/// </para>
/// </summary>
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
	private readonly ILogger<ProblemDetailsExceptionHandler> _logger;
	private readonly IHostEnvironment _environment;

	public ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger, IHostEnvironment environment)
	{
		_logger = logger;
		_environment = environment;
	}

	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		// The correlation id (X13) is on TraceIdentifier — log it with the exception so a 500 in the response can be
		// tied back to the full server-side detail.
		_logger.LogError(exception, "Unhandled exception (correlationId={CorrelationId})", httpContext.TraceIdentifier);

		var problem = new ProblemDetails
		{
			Status = StatusCodes.Status500InternalServerError,
			Title = "An unexpected error occurred.",
			Type = "https://httpstatuses.io/500",
			Instance = httpContext.Request.Path,
		};

		// Correlate the client-visible error with the server logs without exposing internals.
		problem.Extensions["traceId"] = httpContext.TraceIdentifier;

		// Stack traces / messages are operator data — only surface them in local development.
		if (_environment.IsDevelopment())
			problem.Detail = exception.ToString();

		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
		// Pass the content type explicitly: WriteAsJsonAsync would otherwise reset it to "application/json".
		await httpContext.Response
			.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken)
			.ConfigureAwait(false);

		// We fully handled the response — stop the exception-handler pipeline.
		return true;
	}
}
