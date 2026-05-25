using BeDemo.Api.Models;

namespace BeDemo.Api.Utils;

/// <summary>
/// Portal vs operator list visibility for moderated user content on Face detail (admin §1.1).
/// </summary>
public static class OperatorContentListFilters
{
	/// <summary>
	/// When <paramref name="operatorInventory"/> is true (CanManageAllFaces), skip Approved-only and public-album rules.
	/// </summary>
	public static IQueryable<Album> ApplyAlbumPortalVisibility(
		IQueryable<Album> query,
		bool operatorInventory,
		string userId)
	{
		if (operatorInventory)
			return query;
		return query
			.Where(a => a.ApprovalStatus == ContentApprovalStatus.Approved)
			.Where(a => a.AlbumType == AlbumTypeEnum.Public || a.CreatorId == userId);
	}

	public static IQueryable<Reel> ApplyReelPortalVisibility(
		IQueryable<Reel> query,
		bool operatorInventory)
	{
		if (operatorInventory)
			return query;
		return query.Where(r => r.ApprovalStatus == ContentApprovalStatus.Approved);
	}

	public static IQueryable<Blog> ApplyBlogPortalVisibility(
		IQueryable<Blog> query,
		bool operatorInventory)
	{
		if (operatorInventory)
			return query;
		return query.Where(b => b.ApprovalStatus == ContentApprovalStatus.Approved);
	}

}
