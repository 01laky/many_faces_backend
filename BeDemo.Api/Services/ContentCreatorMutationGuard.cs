using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using Microsoft.AspNetCore.Mvc;

namespace BeDemo.Api.Services;

/// <summary>Creator edit/delete guards shared by albums, blogs, and reels.</summary>
public static class ContentCreatorMutationGuard
{
	public const string AlbumsContentKind = "albums";
	public const string BlogsContentKind = "blogs";
	public const string ReelsContentKind = "reels";

	public static IActionResult? TryConflictIfNotEditable(ContentApprovalStatus status, string contentKind)
	{
		if (ContentModerationHelpers.IsCreatorEditable(status))
			return null;
		return ApiErrorResponses.Conflict(
			$"Only pending or rejected {contentKind} can be edited by the creator");
	}

	public static IActionResult? TryConflictIfNotDeletable(ContentApprovalStatus status, string contentKind)
	{
		if (ContentModerationHelpers.IsCreatorDeletable(status))
			return null;
		return ApiErrorResponses.Conflict(
			$"Only pending or rejected {contentKind} can be deleted by the creator");
	}
}
