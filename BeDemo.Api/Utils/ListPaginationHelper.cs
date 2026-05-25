namespace BeDemo.Api.Utils;

/// <summary>Pagination envelope helpers for admin list endpoints.</summary>
public static class ListPaginationHelper
{
	/// <summary>Clamp 1-based page when filters shrink result set (§1.9).</summary>
	public static (int Page, int TotalPages) ClampPage(int page, int pageSize, int totalCount)
	{
		var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)Math.Max(1, pageSize)));
		var clamped = Math.Min(Math.Max(1, page), totalPages);
		return (clamped, totalPages);
	}

	/// <summary>Standard admin list JSON shape consumed by many_faces_admin hooks (§5.6).</summary>
	public static object BuildEnvelope<T>(IReadOnlyList<T> items, int page, int pageSize, int totalCount, int totalPages) =>
		new
		{
			items,
			page,
			pageSize,
			totalCount,
			totalPages,
		};
}
