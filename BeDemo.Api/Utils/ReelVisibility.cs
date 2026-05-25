using BeDemo.Api.Models;

namespace BeDemo.Api.Utils;

public static class ReelVisibility
{
	/// <summary>No <see cref="ReelFace"/> rows → visible on every face; otherwise only listed faces.</summary>
	public static bool IsVisibleForFace(Reel reel, int? faceId)
	{
		if (reel.ReelFaces == null || reel.ReelFaces.Count == 0)
			return true;

		return faceId.HasValue && reel.ReelFaces.Any(rf => rf.FaceId == faceId.Value);
	}
}
