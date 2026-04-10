using BeDemo.Api.Utils;

namespace BeDemo.Api.Services;

/// <summary>
/// Default <see cref="IFaceScopeContext"/> backed by <see cref="IHttpContextAccessor"/>.
/// </summary>
public class FaceScopeContext : IFaceScopeContext
{
    public FaceScopeContext(IHttpContextAccessor httpContextAccessor)
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx?.Items.TryGetValue(FaceScopeConstants.RequestFaceIdItemKey, out var idObj) == true &&
            idObj is int fid)
        {
            IsAvailable = true;
            FaceId = fid;
            FaceIndex = ctx.Items[FaceScopeConstants.RequestFaceIndexItemKey] as string ?? string.Empty;
            IsPublicFace = ctx.Items.TryGetValue(FaceScopeConstants.RequestFaceIsPublicItemKey, out var p) && p is true;
            IsAdminFaceScope = ctx.Items.TryGetValue(FaceScopeConstants.RequestFaceIsAdminScopeItemKey, out var a) && a is true;
        }
        else
        {
            IsAvailable = false;
            FaceId = 0;
            FaceIndex = string.Empty;
            IsPublicFace = false;
            IsAdminFaceScope = false;
        }
    }

    /// <inheritdoc />
    public bool IsAvailable { get; }

    /// <inheritdoc />
    public int FaceId { get; }

    /// <inheritdoc />
    public string FaceIndex { get; }

    /// <inheritdoc />
    public bool IsPublicFace { get; }

    /// <inheritdoc />
    public bool IsAdminFaceScope { get; }

    /// <inheritdoc />
    public int ResolveDataFaceId(int? queryFaceId)
    {
        if (IsAdminFaceScope && queryFaceId.HasValue && queryFaceId.Value > 0)
            return queryFaceId.Value;
        return FaceId;
    }
}
