using BeDemo.Api.Services;

namespace BeDemo.Api.Tests.Performance;

internal sealed class PerformanceTestFaceScope : IFaceScopeContext
{
	public bool IsAvailable { get; init; } = true;
	public int FaceId { get; init; } = 1;
	public string FaceIndex { get; init; } = "public";
	public bool IsPublicFace { get; init; } = true;
	public bool IsAdminFaceScope { get; init; }
	public int ResolveDataFaceId(int? queryFaceId) => FaceId;
}
