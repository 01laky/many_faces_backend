using BeDemo.Api.Models;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Utils;

/// <summary>EF-safe sort application for admin list endpoints (whitelist validated upstream).</summary>
public static class ListSortApplicators
{
	/// <summary>Maps validated <c>sortBy</c> to EF <c>OrderBy</c>; default newest first when sort omitted.</summary>
	public static IOrderedQueryable<ApplicationUser> ApplyUsersSort(
		IQueryable<ApplicationUser> query,
		string? sortBy,
		string? sortDir)
	{
		var desc = SortRules.IsDescending(sortDir);
		return (sortBy?.ToLowerInvariant()) switch
		{
			"id" => desc ? query.OrderByDescending(u => u.Id) : query.OrderBy(u => u.Id),
			"email" => desc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
			"firstname" => desc
				? query.OrderByDescending(u => u.FirstName ?? string.Empty)
				: query.OrderBy(u => u.FirstName ?? string.Empty),
			"lastname" => desc
				? query.OrderByDescending(u => u.LastName ?? string.Empty)
				: query.OrderBy(u => u.LastName ?? string.Empty),
			"createdat" => desc ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
			_ => query.OrderByDescending(u => u.CreatedAt),
		};
	}

	public static IOrderedQueryable<Face> ApplyFacesSort(IQueryable<Face> query, string? sortBy, string? sortDir)
	{
		var desc = SortRules.IsDescending(sortDir);
		return (sortBy?.ToLowerInvariant()) switch
		{
			"id" => desc ? query.OrderByDescending(f => f.Id) : query.OrderBy(f => f.Id),
			"index" => desc ? query.OrderByDescending(f => f.Index) : query.OrderBy(f => f.Index),
			"title" => desc ? query.OrderByDescending(f => f.Title) : query.OrderBy(f => f.Title),
			"ispublic" => desc ? query.OrderByDescending(f => f.IsPublic) : query.OrderBy(f => f.IsPublic),
			"createdat" => desc ? query.OrderByDescending(f => f.CreatedAt) : query.OrderBy(f => f.CreatedAt),
			"updatedat" => desc
				? query.OrderByDescending(f => f.UpdatedAt ?? DateTime.MinValue)
				: query.OrderBy(f => f.UpdatedAt ?? DateTime.MinValue),
			_ => query.OrderBy(f => f.Index),
		};
	}

	/// <summary>CMS pages per face; default stable order by index then name.</summary>
	public static IOrderedQueryable<Page> ApplyPagesSort(IQueryable<Page> query, string? sortBy, string? sortDir)
	{
		var desc = SortRules.IsDescending(sortDir);
		return (sortBy?.ToLowerInvariant()) switch
		{
			"id" => desc ? query.OrderByDescending(p => p.Id) : query.OrderBy(p => p.Id),
			"name" => desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
			"path" => desc ? query.OrderByDescending(p => p.Path) : query.OrderBy(p => p.Path),
			"index" => desc ? query.OrderByDescending(p => p.Index) : query.OrderBy(p => p.Index),
			"createdat" => desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
			"updatedat" => desc
				? query.OrderByDescending(p => p.UpdatedAt ?? DateTime.MinValue)
				: query.OrderBy(p => p.UpdatedAt ?? DateTime.MinValue),
			_ => query.OrderBy(p => p.Index).ThenBy(p => p.Name),
		};
	}

	public static IOrderedQueryable<Album> ApplyAlbumsSort(IQueryable<Album> query, string? sortBy, string? sortDir)
	{
		var desc = SortRules.IsDescending(sortDir);
		return (sortBy?.ToLowerInvariant()) switch
		{
			"id" => desc ? query.OrderByDescending(a => a.Id) : query.OrderBy(a => a.Id),
			"title" => desc ? query.OrderByDescending(a => a.Title) : query.OrderBy(a => a.Title),
			"createdat" => desc ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
			"updatedat" => desc
				? query.OrderByDescending(a => a.UpdatedAt ?? DateTime.MinValue)
				: query.OrderBy(a => a.UpdatedAt ?? DateTime.MinValue),
			"approvalstatus" => desc
				? query.OrderByDescending(a => a.ApprovalStatus)
				: query.OrderBy(a => a.ApprovalStatus),
			"mediatype" => desc ? query.OrderByDescending(a => a.MediaType) : query.OrderBy(a => a.MediaType),
			"albumtype" => desc ? query.OrderByDescending(a => a.AlbumType) : query.OrderBy(a => a.AlbumType),
			_ => query.OrderByDescending(a => a.CreatedAt),
		};
	}

	public static IOrderedQueryable<Reel> ApplyReelsSort(IQueryable<Reel> query, string? sortBy, string? sortDir)
	{
		var desc = SortRules.IsDescending(sortDir);
		return (sortBy?.ToLowerInvariant()) switch
		{
			"id" => desc ? query.OrderByDescending(r => r.Id) : query.OrderBy(r => r.Id),
			"title" => desc ? query.OrderByDescending(r => r.Title) : query.OrderBy(r => r.Title),
			"createdat" => desc ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
			"updatedat" => desc
				? query.OrderByDescending(r => r.UpdatedAt ?? DateTime.MinValue)
				: query.OrderBy(r => r.UpdatedAt ?? DateTime.MinValue),
			"approvalstatus" => desc
				? query.OrderByDescending(r => r.ApprovalStatus)
				: query.OrderBy(r => r.ApprovalStatus),
			_ => query.OrderByDescending(r => r.CreatedAt),
		};
	}

	public static IOrderedQueryable<Blog> ApplyBlogsSort(IQueryable<Blog> query, string? sortBy, string? sortDir)
	{
		var desc = SortRules.IsDescending(sortDir);
		return (sortBy?.ToLowerInvariant()) switch
		{
			"id" => desc ? query.OrderByDescending(b => b.Id) : query.OrderBy(b => b.Id),
			"title" => desc ? query.OrderByDescending(b => b.Title) : query.OrderBy(b => b.Title),
			"createdat" => desc ? query.OrderByDescending(b => b.CreatedAt) : query.OrderBy(b => b.CreatedAt),
			"updatedat" => desc
				? query.OrderByDescending(b => b.UpdatedAt ?? DateTime.MinValue)
				: query.OrderBy(b => b.UpdatedAt ?? DateTime.MinValue),
			"approvalstatus" => desc
				? query.OrderByDescending(b => b.ApprovalStatus)
				: query.OrderBy(b => b.ApprovalStatus),
			_ => query.OrderByDescending(b => b.CreatedAt),
		};
	}

	public static IOrderedQueryable<Story> ApplyStoriesSort(IQueryable<Story> query, string? sortBy, string? sortDir)
	{
		var desc = SortRules.IsDescending(sortDir);
		return (sortBy?.ToLowerInvariant()) switch
		{
			"id" => desc ? query.OrderByDescending(s => s.Id) : query.OrderBy(s => s.Id),
			"title" => desc ? query.OrderByDescending(s => s.Title) : query.OrderBy(s => s.Title),
			"createdat" => desc ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt),
			"publishedat" => desc
				? query.OrderByDescending(s => s.PublishedAt ?? DateTime.MinValue)
				: query.OrderBy(s => s.PublishedAt ?? DateTime.MinValue),
			"ispublished" => desc
				? query.OrderByDescending(s => s.State == StoryState.Published)
				: query.OrderBy(s => s.State == StoryState.Published),
			_ => query.OrderByDescending(s => s.CreatedAt),
		};
	}

	public static IOrderedQueryable<FaceChatRoom> ApplyFaceChatRoomsSort(
		IQueryable<FaceChatRoom> query,
		string? sortBy,
		string? sortDir)
	{
		var desc = SortRules.IsDescending(sortDir);
		return (sortBy?.ToLowerInvariant()) switch
		{
			"id" => desc ? query.OrderByDescending(r => r.Id) : query.OrderBy(r => r.Id),
			"title" => desc ? query.OrderByDescending(r => r.Title) : query.OrderBy(r => r.Title),
			"createdat" => desc ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
			"ispublic" => desc ? query.OrderByDescending(r => r.IsPublic) : query.OrderBy(r => r.IsPublic),
			_ => query.OrderByDescending(r => r.LastMessageAt ?? r.CreatedAt),
		};
	}

	public static IOrderedQueryable<FaceVideoLounge> ApplyFaceVideoLoungesSort(
		IQueryable<FaceVideoLounge> query,
		string? sortBy,
		string? sortDir)
	{
		var desc = SortRules.IsDescending(sortDir);
		return (sortBy?.ToLowerInvariant()) switch
		{
			"id" => desc ? query.OrderByDescending(r => r.Id) : query.OrderBy(r => r.Id),
			"title" => desc ? query.OrderByDescending(r => r.Title) : query.OrderBy(r => r.Title),
			"createdat" => desc ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
			"ispublic" => desc ? query.OrderByDescending(r => r.IsPublic) : query.OrderBy(r => r.IsPublic),
			_ => query.OrderByDescending(r => r.CreatedAt),
		};
	}
}
