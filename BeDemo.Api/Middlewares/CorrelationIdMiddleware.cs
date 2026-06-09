namespace BeDemo.Api.Middlewares;

/// <summary>
/// Correlation-id middleware (backend-refactor X13). Gives every request a stable id that is:
/// <list type="bullet">
///   <item>taken from an inbound <c>X-Correlation-Id</c> / <c>X-Request-Id</c> header when the caller supplied a
///         <em>safe</em> one (so a trace can be followed across the gateway / frontend / worker hops), otherwise a
///         freshly generated id;</item>
///   <item>pushed into the logging scope as <c>CorrelationId</c> so every log line for the request carries it;</item>
///   <item>mirrored back on the response <c>X-Correlation-Id</c> header so clients (and integration tests) can read
///         the id the server used;</item>
///   <item>copied into <see cref="HttpContext.TraceIdentifier"/> so framework-emitted diagnostics line up with ours.</item>
/// </list>
/// The inbound value is validated (bounded length + conservative charset) before it is echoed or logged — an
/// unvalidated client header would be a log-injection / response-header-reflection vector, so an unsafe value is
/// discarded and a server id is generated instead.
/// </summary>
public sealed class CorrelationIdMiddleware
{
	/// <summary>Primary header read and always written back.</summary>
	public const string CorrelationHeader = "X-Correlation-Id";

	/// <summary>Alternate inbound header accepted for interop with gateways/proxies that use this spelling.</summary>
	public const string RequestIdHeader = "X-Request-Id";

	/// <summary>Upper bound on an accepted inbound id — long enough for a GUID/trace id, short enough to bound log/header size.</summary>
	internal const int MaxLength = 128;

	private readonly RequestDelegate _next;
	private readonly ILogger<CorrelationIdMiddleware> _logger;

	public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		var correlationId = ResolveCorrelationId(context);

		// Align framework diagnostics + expose to downstream code (services can read HttpContext.TraceIdentifier).
		context.TraceIdentifier = correlationId;

		// Echo on the response. Use OnStarting so we set it even if a later component has begun writing; guard against
		// duplicates if something upstream already wrote the header.
		context.Response.OnStarting(() =>
		{
			context.Response.Headers[CorrelationHeader] = correlationId;
			return Task.CompletedTask;
		});

		// Scope every log line emitted for this request with the id.
		using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
		{
			await _next(context);
		}
	}

	/// <summary>Pick a safe inbound id (primary then alternate header) or generate a fresh server id.</summary>
	private static string ResolveCorrelationId(HttpContext context)
	{
		if (TryGetSafeHeader(context, CorrelationHeader, out var fromCorrelation))
			return fromCorrelation;

		if (TryGetSafeHeader(context, RequestIdHeader, out var fromRequest))
			return fromRequest;

		return Guid.NewGuid().ToString("N");
	}

	private static bool TryGetSafeHeader(HttpContext context, string headerName, out string value)
	{
		value = string.Empty;

		if (!context.Request.Headers.TryGetValue(headerName, out var raw))
			return false;

		// Multiple header values would be ambiguous to echo back — only trust a single, well-formed value.
		if (raw.Count != 1)
			return false;

		var candidate = raw.ToString();
		if (!IsSafeCorrelationId(candidate))
			return false;

		value = candidate;
		return true;
	}

	/// <summary>
	/// Conservative allow-list: non-empty, <= <see cref="MaxLength"/>, and only URL/identifier-safe characters
	/// (letters, digits, <c>-</c>, <c>_</c>, <c>.</c>). Rejects whitespace, CR/LF, and control characters so the value
	/// can never inject a new log line or smuggle a second response header.
	/// </summary>
	internal static bool IsSafeCorrelationId(string candidate)
	{
		if (string.IsNullOrEmpty(candidate) || candidate.Length > MaxLength)
			return false;

		foreach (var c in candidate)
		{
			var isAllowed =
				(c >= 'A' && c <= 'Z') ||
				(c >= 'a' && c <= 'z') ||
				(c >= '0' && c <= '9') ||
				c is '-' or '_' or '.';

			if (!isAllowed)
				return false;
		}

		return true;
	}
}
