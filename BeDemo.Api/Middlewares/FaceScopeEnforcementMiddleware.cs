/*
 * FaceScopeEnforcementMiddleware.cs — runs AFTER JWT authentication.
 *
 * Responsibilities:
 * 1) For non-exempt paths, require that RoutingMiddleware attached face scope (Items).
 * 2) For private faces (IsPublic == false), require an authenticated principal.
 * 3) For tenant (non-admin) scopes, verify that query faceId / requestFaceID (if duplicated)
 *    still match the scoped face id — catches bugs or bypass attempts after rewrite.
 * 4) For admin scope, if faceId is present, ensure it references an existing Face row
 *    (optional hardening; avoids silently treating invalid ids as filters).
 */

using BeDemo.Api.Data;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Middlewares;

/// <summary>
/// Enforces face scope consistency after <see cref="Microsoft.AspNetCore.Authentication.AuthenticationMiddleware"/>.
/// </summary>
public class FaceScopeEnforcementMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<FaceScopeEnforcementMiddleware> _logger;

	public FaceScopeEnforcementMiddleware(RequestDelegate next, ILogger<FaceScopeEnforcementMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
	{
		var path = context.Request.Path.Value ?? string.Empty;

		if (Routing.IsExemptFromFaceScope(path))
		{
			await _next(context);
			return;
		}

		if (!context.Items.TryGetValue(FaceScopeConstants.RequestFaceIdItemKey, out var idObj) ||
			idObj is not int scopedFaceId)
		{
			// Should not happen if RoutingMiddleware ran and path was not exempt; treat as server misconfiguration.
			_logger.LogWarning("Face scope missing for path {Path}", path);
			context.Response.StatusCode = StatusCodes.Status400BadRequest;
			context.Response.ContentType = "text/plain; charset=utf-8";
			await context.Response.WriteAsync("Face scope is required for this path.");
			return;
		}

		var isPublic = context.Items.TryGetValue(FaceScopeConstants.RequestFaceIsPublicItemKey, out var pubObj) &&
					   pubObj is true;

		if (!isPublic && !IsAnonymousMailerPilotLink(context) && context.User?.Identity?.IsAuthenticated != true)
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			context.Response.ContentType = "text/plain; charset=utf-8";
			await context.Response.WriteAsync("Authentication required for this face.");
			return;
		}

		var isAdminScope = context.Items.TryGetValue(FaceScopeConstants.RequestFaceIsAdminScopeItemKey, out var adm) &&
						   adm is true;

		if (!isAdminScope)
		{
			if (!QueryIntegersAllMatch(context, "faceId", scopedFaceId) ||
				!QueryIntegersAllMatch(context, "requestFaceID", scopedFaceId))
			{
				_logger.LogWarning(
					"Tenant scope mismatch: scoped face {ScopedFaceId} vs query on {Path}",
					scopedFaceId,
					path);
				context.Response.StatusCode = StatusCodes.Status403Forbidden;
				context.Response.ContentType = "text/plain; charset=utf-8";
				await context.Response.WriteAsync("Face scope mismatch: query parameters do not match URL prefix.");
				return;
			}
		}
		else
		{
			// Admin scope: requestFaceID must still be the admin face id; optional faceId may target another tenant.
			if (!QueryIntegersAllMatch(context, "requestFaceID", scopedFaceId))
			{
				context.Response.StatusCode = StatusCodes.Status403Forbidden;
				await context.Response.WriteAsync("requestFaceID does not match admin scope.");
				return;
			}

			if (context.Request.Query.TryGetValue("faceId", out var faceIdVals))
			{
				foreach (var v in faceIdVals)
				{
					if (!int.TryParse(v, out var fid) || fid <= 0)
					{
						context.Response.StatusCode = StatusCodes.Status400BadRequest;
						await context.Response.WriteAsync("Invalid faceId query value.");
						return;
					}

					var exists = await db.Faces.AsNoTracking().AnyAsync(f => f.Id == fid);
					if (!exists)
					{
						context.Response.StatusCode = StatusCodes.Status400BadRequest;
						await context.Response.WriteAsync("faceId does not reference an existing face.");
						return;
					}
				}
			}
		}

		await _next(context);
	}

	/// <summary>
	/// GET <c>*/api/admin/mailer/pilot-link</c> is linked from operator pilot mail; mail clients have no JWT cookie.
	/// The URL still uses a face prefix (e.g. <c>/admin/api/...</c>) so routing stays consistent.
	/// </summary>
	private static bool IsAnonymousMailerPilotLink(HttpContext context)
	{
		if (!HttpMethods.IsGet(context.Request.Method))
		{
			return false;
		}

		var path = context.Request.Path.Value ?? string.Empty;
		return path.Contains("/api/admin/mailer/pilot-link", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Returns false if the query contains the key with any value that does not parse to <paramref name="expected"/>.
	/// Missing key is OK (caller may rely on server-injected single value elsewhere).
	/// </summary>
	private static bool QueryIntegersAllMatch(HttpContext context, string key, int expected)
	{
		if (!context.Request.Query.TryGetValue(key, out var values))
			return true;

		foreach (var v in values)
		{
			if (!int.TryParse(v, out var parsed) || parsed != expected)
				return false;
		}

		return true;
	}
}
