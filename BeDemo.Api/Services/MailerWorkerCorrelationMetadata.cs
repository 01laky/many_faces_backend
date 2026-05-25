using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace BeDemo.Api.Services;

/// <summary>
/// Copies inbound HTTP correlation headers into outbound gRPC <see cref="Metadata"/> (smtp-mailer prompt §5.6).
/// Keys are lowercase ASCII; gRPC metadata keys are matched case-insensitively on the wire.
/// </summary>
public static class MailerWorkerCorrelationMetadata
{
	public const string RequestIdKey = "x-request-id";
	public const string TraceParentKey = "traceparent";
	public const string TraceStateKey = "tracestate";

	public static void AppendFromHttpHeaders(IHeaderDictionary? requestHeaders, Metadata metadata)
	{
		if (requestHeaders is null)
		{
			return;
		}

		TryAddSanitized(metadata, RequestIdKey, requestHeaders, "X-Request-Id");
		TryAddSanitized(metadata, TraceParentKey, requestHeaders, "traceparent");
		TryAddSanitized(metadata, TraceStateKey, requestHeaders, "tracestate");
	}

	private static void TryAddSanitized(Metadata metadata, string metadataKey, IHeaderDictionary headers, string httpHeaderName)
	{
		if (!headers.TryGetValue(httpHeaderName, out StringValues sv) || StringValues.IsNullOrEmpty(sv))
		{
			return;
		}

		var raw = sv.Count > 0 ? sv[0] : null;
		if (string.IsNullOrWhiteSpace(raw))
		{
			return;
		}

		var trimmed = raw.Trim();
		if (!IsSafeSingleLineAscii(trimmed))
		{
			return;
		}

		metadata.Add(metadataKey, trimmed);
	}

	private static bool IsSafeSingleLineAscii(string value)
	{
		if (value.Length > 256)
		{
			return false;
		}

		foreach (var c in value)
		{
			if (c == '\r' || c == '\n' || c == '\0' || c > 127)
			{
				return false;
			}
		}

		return true;
	}
}
