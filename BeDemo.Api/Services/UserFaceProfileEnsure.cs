using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>Shared create-or-get for <see cref="UserFaceProfile"/> (Profile + Faces controllers).</summary>
public static class UserFaceProfileEnsure
{
	public sealed class Options
	{
		public bool MarkVisited { get; init; }
		public bool? IsActive { get; init; }
		public bool? FaceRoleIntroCompleted { get; init; }

		public static Options Passive => new();

		public static Options ForVisit => new() { MarkVisited = true };

		public static Options ForFaceRole(bool isActive, bool faceRoleIntroCompleted) =>
			new()
			{
				IsActive = isActive,
				FaceRoleIntroCompleted = faceRoleIntroCompleted,
			};

		internal void ApplyToExisting(UserFaceProfile ufp)
		{
			if (MarkVisited)
				ufp.Visited = true;
			if (IsActive.HasValue)
				ufp.IsActive = IsActive.Value;
			if (FaceRoleIntroCompleted.HasValue)
				ufp.FaceRoleIntroCompleted = FaceRoleIntroCompleted.Value;
			ufp.UpdatedAt = DateTime.UtcNow;
		}

		internal UserFaceProfile CreateNew(int userProfileId, int faceId) =>
			new()
			{
				UserProfileId = userProfileId,
				FaceId = faceId,
				IsActive = IsActive ?? false,
				Visited = MarkVisited,
				FaceRoleIntroCompleted = FaceRoleIntroCompleted ?? false,
				CreatedAt = DateTime.UtcNow,
			};
	}

	public static async Task<UserFaceProfile> GetOrCreateAsync(
		ApplicationDbContext context,
		int userProfileId,
		int faceId,
		Options options,
		CancellationToken cancellationToken = default)
	{
		var ufp = await context.UserFaceProfiles
			.FirstOrDefaultAsync(
				x => x.UserProfileId == userProfileId && x.FaceId == faceId,
				cancellationToken);

		if (ufp != null)
		{
			options.ApplyToExisting(ufp);
			return ufp;
		}

		ufp = options.CreateNew(userProfileId, faceId);
		context.UserFaceProfiles.Add(ufp);
		return ufp;
	}
}
