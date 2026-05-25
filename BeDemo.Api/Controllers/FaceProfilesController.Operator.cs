using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Utils;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Controllers;

public partial class FaceProfilesController
{
	/// <summary>GET — list face directory (inner-join UserFaceProfile; operator search/sort).</summary>
	[HttpGet]
	[Microsoft.AspNetCore.Authorization.AllowAnonymous]
	public async Task<IActionResult> ListProfiles(
		int faceId,
		[FromQuery] FaceProfileListQuery listQuery,
		CancellationToken ct = default)
	{
		var face = await GetFaceAsync(faceId, ct);
		if (face == null)
			return NotFound(new { error = "Face not found" });

		var operatorInventory = CanManageAllFaces();
		if (!operatorInventory &&
			!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, CurrentUserId, ct))
			return VisibilityDenied();

		var hostName = UserRole.FaceRoleNames.FaceHost;
		var baseQuery =
			from ufp in _context.UserFaceProfiles.AsNoTracking()
			join up in _context.UserProfiles.AsNoTracking() on ufp.UserProfileId equals up.Id
			where ufp.FaceId == faceId
			where _context.UserFaceRoles.Any(ufr =>
				ufr.UserId == up.UserId &&
				ufr.FaceId == faceId &&
				_context.UserRoles.Any(ur =>
					ur.Id == ufr.UserRoleId &&
					ur.Scope == RoleScope.Face &&
					ur.Name != hostName))
			select new { ufp, up };

		if (!string.IsNullOrWhiteSpace(listQuery.Search))
		{
			var term = listQuery.Search.Trim();
			var guidLike = term.Length >= 8 && term.Contains('-', StringComparison.Ordinal);
			baseQuery = baseQuery.Where(x =>
				(x.ufp.DisplayName != null && EF.Functions.ILike(x.ufp.DisplayName, $"%{term}%")) ||
				// Nickname is optional; guard it explicitly so the generated SQL keeps null rows
				// searchable through the other fields without nullable-analysis warnings.
				(x.up.Nickname != null && EF.Functions.ILike(x.up.Nickname, $"%{term}%")) ||
				(guidLike && EF.Functions.ILike(x.up.UserId, $"%{term}%")));
		}

		var desc = SortRules.IsDescending(listQuery.SortDir);
		var ordered = (listQuery.SortBy?.ToLowerInvariant()) switch
		{
			"userid" => desc
				? baseQuery.OrderByDescending(x => x.up.UserId)
				: baseQuery.OrderBy(x => x.up.UserId),
			"displayname" => desc
				? baseQuery.OrderByDescending(x => x.ufp.DisplayName ?? x.up.Nickname)
				: baseQuery.OrderBy(x => x.ufp.DisplayName ?? x.up.Nickname),
			"joinedat" => desc
				? baseQuery.OrderByDescending(x => x.ufp.CreatedAt)
				: baseQuery.OrderBy(x => x.ufp.CreatedAt),
			"lastvisitedat" => desc
				? baseQuery.OrderByDescending(x => x.ufp.Visited).ThenByDescending(x => x.ufp.UpdatedAt ?? x.ufp.CreatedAt)
				: baseQuery.OrderBy(x => x.ufp.Visited).ThenBy(x => x.ufp.UpdatedAt ?? x.ufp.CreatedAt),
			_ => baseQuery.OrderBy(x => x.ufp.DisplayName ?? x.up.Nickname),
		};

		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;
		var totalCount = await ordered.CountAsync(ct);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var pageRows = await ordered
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(x => new
			{
				ProfileId = x.ufp.Id,
				x.up.UserId,
				x.ufp.DisplayName,
				x.up.Nickname,
				FaceAvatar = x.ufp.AvatarUrl,
				GlobalAvatar = x.up.AvatarUrl,
			})
			.ToListAsync(ct);

		var profileIds = pageRows.Select(r => r.ProfileId).ToList();
		var commentCounts = await _context.UserFaceProfileComments.AsNoTracking()
			.Where(c => profileIds.Contains(c.UserFaceProfileId))
			.GroupBy(c => c.UserFaceProfileId)
			.Select(g => new { g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.Key, x => x.Count, ct);
		var likeCounts = await _context.UserFaceProfileLikes.AsNoTracking()
			.Where(l => profileIds.Contains(l.UserFaceProfileId))
			.GroupBy(l => l.UserFaceProfileId)
			.Select(g => new { g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.Key, x => x.Count, ct);
		var reviewCounts = face.AllowRecensions
			? await _context.UserFaceProfileReviews.AsNoTracking()
				.Where(r => profileIds.Contains(r.UserFaceProfileId))
				.GroupBy(r => r.UserFaceProfileId)
				.Select(g => new { g.Key, Count = g.Count() })
				.ToDictionaryAsync(x => x.Key, x => x.Count, ct)
			: new Dictionary<int, int>();

		var bannedSet = (await _context.UserFaceModerations.AsNoTracking()
			.Where(m => m.FaceId == faceId && m.LiftedAt == null)
			.Select(m => m.UserId)
			.ToListAsync(ct)).ToHashSet();

		var items = pageRows.Select(row =>
		{
			var display = row.DisplayName?.Trim();
			if (string.IsNullOrEmpty(display))
				display = row.Nickname;
			var avatar = !string.IsNullOrWhiteSpace(row.FaceAvatar) ? row.FaceAvatar : row.GlobalAvatar;
			return new
			{
				userId = row.UserId,
				displayName = display,
				avatarUrl = _uploadUrls.ToAbsoluteSignedUrl(avatar, Request.Scheme, Request.Host.Value!),
				commentsCount = commentCounts.GetValueOrDefault(row.ProfileId),
				likesCount = likeCounts.GetValueOrDefault(row.ProfileId),
				reviewsCount = face.AllowRecensions ? reviewCounts.GetValueOrDefault(row.ProfileId) : 0,
				isFaceBanned = bannedSet.Contains(row.UserId),
			};
		}).ToList();

		return Ok(ListPaginationHelper.BuildEnvelope(items, page, pageSize, totalCount, totalPages));
	}
}
