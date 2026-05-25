namespace BeDemo.Api.Middlewares;

/// <summary>
/// Adds baseline HTTP security headers (H1): limits MIME sniffing, clickjacking, and default powerful feature access.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
	private readonly RequestDelegate _next;

	public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

	public Task InvokeAsync(HttpContext context)
	{
		var headers = context.Response.Headers;
		headers["X-Content-Type-Options"] = "nosniff";
		headers["X-Frame-Options"] = "DENY";
		headers["Referrer-Policy"] = "no-referrer";
		headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
		// Minimal CSP for a JSON API (no inline scripts on this host): blocks accidental script execution if MIME is wrong.
		headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
		return _next(context);
	}
}
