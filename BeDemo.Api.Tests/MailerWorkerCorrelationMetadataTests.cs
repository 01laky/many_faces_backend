using BeDemo.Api.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class MailerWorkerCorrelationMetadataTests
{
    [Fact]
    public void AppendFromHttpHeaders_copies_known_headers_with_lowercase_metadata_keys()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Request-Id"] = "req-abc";
        http.Request.Headers["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        http.Request.Headers["tracestate"] = "vendor=v1";

        var md = new Metadata();
        MailerWorkerCorrelationMetadata.AppendFromHttpHeaders(http.Request.Headers, md);

        Assert.Equal("req-abc", GetFirst(md, MailerWorkerCorrelationMetadata.RequestIdKey));
        Assert.Equal(
            "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            GetFirst(md, MailerWorkerCorrelationMetadata.TraceParentKey));
        Assert.Equal("vendor=v1", GetFirst(md, MailerWorkerCorrelationMetadata.TraceStateKey));
    }

    [Fact]
    public void AppendFromHttpHeaders_skips_values_with_illegal_control_chars()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Request-Id"] = "bad\r\ninjection";

        var md = new Metadata();
        MailerWorkerCorrelationMetadata.AppendFromHttpHeaders(http.Request.Headers, md);

        Assert.Null(GetFirst(md, MailerWorkerCorrelationMetadata.RequestIdKey));
    }

    private static string? GetFirst(Metadata md, string key)
    {
        foreach (var e in md)
        {
            if (string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return e.Value;
            }
        }

        return null;
    }
}
