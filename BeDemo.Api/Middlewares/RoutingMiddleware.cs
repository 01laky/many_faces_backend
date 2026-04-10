/*
 * RoutingMiddleware.cs — first pipeline stage for face-prefixed URLs.
 *
 * Incoming: /{faceKebab}/api/Controller/...  or  /{faceKebab}/hubs/...
 * Rewritten: /api/Controller/...  or  /hubs/...  with HttpContext.Items populated.
 *
 * Security notes:
 * - Client-supplied query keys faceId / requestFaceID are stripped and re-applied server-side
 *   so callers cannot escalate to another tenant by tacking ?faceId=2 on a /basic/... URL.
 * - In admin scope, an optional faceId query may be preserved to let the admin UI target
 *   another face's data while the URL prefix remains /admin/ (admin JWT + global Admin role still required).
 * - Private-face authentication is enforced later (after JWT) in FaceScopeEnforcementMiddleware.
 */

using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http.Extensions;

namespace BeDemo.Api.Middlewares;

/// <summary>
/// Rewrites face-prefixed paths and attaches resolved face metadata to <see cref="HttpContext.Items"/>.
/// </summary>
public class RoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _memoryCache;
    private static readonly char[] Separator = { '/' };

    public RoutingMiddleware(RequestDelegate next, IMemoryCache memoryCache)
    {
        _next = next;
        _memoryCache = memoryCache;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // System endpoints: no rewrite, no Items — FaceScopeEnforcementMiddleware will skip these too.
        if (Routing.IsExemptFromFaceScope(path))
        {
            await _next(context);
            return;
        }

        // Old clients calling /api/... directly — reject with 400 (distinct from unknown face 403).
        if (Routing.IsReservedPathWithoutFacePrefix(path))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(
                "Face URL prefix is required. Use /{face-prefix}/api/... (e.g. /public/api/...). " +
                "OAuth token and registration stay at /api/oauth2/....");
            return;
        }

        var segments = path.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            // e.g. "/onlyone" — not a valid API or hub path with prefix
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Face path not allowed");
            return;
        }

        var prefix = segments[0];
        var faces = GetFaces(serviceProvider);
        var matchingFace = faces.FirstOrDefault(f => Routing.ConvertToKebabCase(f.Index) == prefix);

        if (matchingFace == null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Face path not allowed");
            return;
        }

        var newPath = "/" + string.Join("/", segments, 1, segments.Length - 1);
        context.Request.Path = newPath;

        // -------------------------------------------------------------------------
        // Query string hardening: strip tenant-spoofing parameters, then re-add trusted values.
        // -------------------------------------------------------------------------
        int? adminPreservedFaceId = null;
        if (FaceScopeConstants.IsAdminFaceIndex(matchingFace.Index) &&
            context.Request.Query.TryGetValue("faceId", out var incomingFaceVals))
        {
            var raw = incomingFaceVals.FirstOrDefault();
            if (int.TryParse(raw, out var parsed) && parsed > 0)
                adminPreservedFaceId = parsed;
        }

        var qb = new QueryBuilder();
        foreach (var kv in context.Request.Query)
        {
            if (kv.Key.Equals("requestFaceID", StringComparison.OrdinalIgnoreCase))
                continue;
            if (kv.Key.Equals("faceId", StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var v in kv.Value)
            {
                if (v != null)
                    qb.Add(kv.Key, v);
            }
        }

        qb.Add("requestFaceID", matchingFace.Id.ToString());

        if (FaceScopeConstants.IsAdminFaceIndex(matchingFace.Index))
        {
            // Admin UI may pass faceId= to operate on a tenant while staying under /admin/ prefix.
            if (adminPreservedFaceId.HasValue)
                qb.Add("faceId", adminPreservedFaceId.Value.ToString());
        }
        else
        {
            // Tenant scope: faceId in query must always equal URL prefix face (defense in depth with enforcement).
            qb.Add("faceId", matchingFace.Id.ToString());
        }

        context.Request.QueryString = qb.ToQueryString();

        // -------------------------------------------------------------------------
        // Trusted scope for downstream middleware, filters, and controllers.
        // -------------------------------------------------------------------------
        context.Items[FaceScopeConstants.RequestFaceIdItemKey] = matchingFace.Id;
        context.Items[FaceScopeConstants.RequestFaceIndexItemKey] = matchingFace.Index;
        context.Items[FaceScopeConstants.RequestFaceIsPublicItemKey] = matchingFace.IsPublic;
        context.Items[FaceScopeConstants.RequestFaceIsAdminScopeItemKey] =
            FaceScopeConstants.IsAdminFaceIndex(matchingFace.Index);

        await _next(context);
    }

    private List<Face> GetFaces(IServiceProvider serviceProvider)
    {
        if (!_memoryCache.TryGetValue("Faces", out List<Face>? faces) || faces == null)
        {
            var faceService = serviceProvider.GetRequiredService<IFaceService>();
            faces = faceService.GetFaces();
            _memoryCache.Set("Faces", faces, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            });
        }

        return faces ?? new List<Face>();
    }
}
