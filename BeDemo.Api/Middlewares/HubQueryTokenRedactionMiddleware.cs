namespace BeDemo.Api.Middlewares;

/// <summary>
/// BSH3-A7: stores a redacted request path+query for diagnostics so SignalR <c>access_token</c> never appears in logs.
/// </summary>
public sealed class HubQueryTokenRedactionMiddleware
{
    /// <summary>HttpContext.Items key — use instead of raw Request.QueryString when logging hub negotiate URLs.</summary>
    public const string RedactedPathAndQueryItemKey = "RedactedPathAndQuery";

    private readonly RequestDelegate _next;

    public HubQueryTokenRedactionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)
            && context.Request.Query.ContainsKey("access_token"))
        {
            context.Items[RedactedPathAndQueryItemKey] =
                $"{context.Request.Path}?access_token=[REDACTED]";
        }

        await _next(context);
    }
}
