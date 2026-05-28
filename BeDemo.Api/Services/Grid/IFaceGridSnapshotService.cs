namespace BeDemo.Api.Services.Grid;

public enum FaceGridSnapshotStatus
{
	Success,
	NotFound,
	Forbidden,
}

public sealed class FaceGridSnapshotResult
{
	public FaceGridSnapshotStatus Status { get; init; }
	public IReadOnlyDictionary<string, object> Blocks { get; init; } = new Dictionary<string, object>();
}

public interface IFaceGridSnapshotService
{
	Task<FaceGridSnapshotResult> GetSnapshotAsync(
		int faceId,
		System.Security.Claims.ClaimsPrincipal user,
		string? userId,
		IReadOnlyList<string> blocks,
		int page,
		int pageSize,
		string requestScheme,
		string requestHost,
		CancellationToken cancellationToken = default);
}
