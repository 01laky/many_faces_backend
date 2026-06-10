using System.Security.Claims;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Faces;

public interface IFacesConfigService
{
	Task<IReadOnlyList<FaceConfigDto>> GetFacesConfigAsync(
		ClaimsPrincipal user,
		string? userId,
		CancellationToken cancellationToken = default);

	void InvalidateAll();
}

public sealed class FacesConfigService : IFacesConfigService
{
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	private readonly IMemoryCache _cache;
	private readonly IOptions<PerformanceOptions> _perfOptions;
	private readonly ILogger<FacesConfigService> _logger;

	public FacesConfigService(
		ApplicationDbContext context,
		IFaceScopeContext faceScope,
		IAccessEvaluator access,
		IMemoryCache cache,
		IOptions<PerformanceOptions> perfOptions,
		ILogger<FacesConfigService> logger)
	{
		_context = context;
		_faceScope = faceScope;
		_access = access;
		_cache = cache;
		_perfOptions = perfOptions;
		_logger = logger;
	}

	private string CacheKey(string? userId, bool isAdmin, bool isPublic, int scopedFaceId)
	{
		var gen = _cache.Get<int?>("faces-config-gen") ?? 0;
		return $"faces-config:{gen}:{isAdmin}:{isPublic}:{scopedFaceId}:{userId ?? "anon"}";
	}

	public void InvalidateAll() =>
		_cache.Set("faces-config-gen", (_cache.Get<int?>("faces-config-gen") ?? 0) + 1);

	public async Task<IReadOnlyList<FaceConfigDto>> GetFacesConfigAsync(
		ClaimsPrincipal user,
		string? userId,
		CancellationToken cancellationToken = default)
	{
		var key = CacheKey(userId, _faceScope.IsAdminFaceScope, _faceScope.IsPublicFace, _faceScope.FaceId);
		if (_cache.TryGetValue(key, out IReadOnlyList<FaceConfigDto>? cached) && cached is not null)
			return cached;

		var faces = await LoadFacesAsync(user, userId, cancellationToken).ConfigureAwait(false);
		var (myFaceRoles, myFaceState) = await LoadUserFaceMetaAsync(userId, cancellationToken).ConfigureAwait(false);

		var facesConfig = faces.Select(f =>
		{
			int? myFaceRoleId = null;
			string? myFaceRoleName = null;
			if (myFaceRoles != null && myFaceRoles.TryGetValue(f.Id, out var role))
			{
				myFaceRoleId = role.RoleId;
				myFaceRoleName = role.RoleName;
			}

			bool? myVisited = null;
			bool? myFaceRoleIntroCompleted = null;
			if (myFaceState != null && myFaceState.TryGetValue(f.Id, out var st))
			{
				myVisited = st.Visited;
				myFaceRoleIntroCompleted = st.FaceRoleIntroCompleted;
			}

			return new FaceConfigDto
			{
				Index = f.Index,
				Id = f.Id,
				Title = f.Title,
				Description = f.Description,
				GradientSettings = f.GradientSettings,
				IsPublic = f.IsPublic,
				Visibility = f.Visibility.ToString(),
				AllowRecensions = f.AllowRecensions,
				ChatRoomsCreate = f.ChatRoomsCreate,
				VideoLoungesCreate = f.VideoLoungesCreate,
				MyFaceRoleId = myFaceRoleId,
				MyFaceRoleName = myFaceRoleName,
				MyVisited = myVisited,
				MyFaceRoleIntroCompleted = myFaceRoleIntroCompleted,
				Pages = f.Pages
					.OrderBy(p => p.Index)
					.Select(p => new FaceConfigPageDto
					{
						Index = p.Index,
						Id = p.Id,
						Name = p.Name,
						Description = p.Description,
						Path = p.Path,
						GridSchema = p.GridSchema,
						PageType = p.PageType != null
							? new FaceConfigPageTypeDto { Id = p.PageType.Id, Index = p.PageType.Index }
							: null,
						RouteTranslations = p.RouteTranslations
							.OrderBy(rt => rt.LanguageCode)
							.Select(rt => new FaceConfigRouteTranslationDto
							{
								LanguageCode = rt.LanguageCode,
								TranslatedRoute = rt.TranslatedRoute,
							}).ToList(),
						CreatedAt = p.CreatedAt,
						UpdatedAt = p.UpdatedAt,
					}).ToList(),
			};
		}).ToList();

		var ttl = TimeSpan.FromSeconds(Math.Max(5, _perfOptions.Value.FacesConfigCacheSeconds));
		_cache.Set(key, facesConfig, ttl);
		_logger.LogInformation("Retrieved faces config with {FaceCount} faces", facesConfig.Count);
		return facesConfig;
	}

	private bool CanManageAllFaces(ClaimsPrincipal user) => _access.CanManageAllFaces(user);

	private bool IsGlobalAdmin(ClaimsPrincipal user) =>
		user.Identity?.IsAuthenticated == true && CanManageAllFaces(user);

	private async Task<List<Face>> LoadFacesAsync(
		ClaimsPrincipal user,
		string? userId,
		CancellationToken cancellationToken)
	{
		IQueryable<Face> BaseQuery() => _context.Faces
			.AsNoTracking()
			.AsSplitQuery()
			.TagIfEnabled(_perfOptions, EfQueryTags.FacesConfig)
			.Include(f => f.Pages)
			.ThenInclude(p => p.PageType)
			.Include(f => f.Pages)
			.ThenInclude(p => p.RouteTranslations);

		if (_faceScope.IsAdminFaceScope)
		{
			if (user.Identity?.IsAuthenticated != true || !CanManageAllFaces(user))
				throw new UnauthorizedAccessException("Admin faces config requires super-admin.");

			return await BaseQuery().OrderBy(f => f.Index).ToListAsync(cancellationToken);
		}

		if (_faceScope.IsPublicFace && user.Identity?.IsAuthenticated != true)
		{
			return await BaseQuery()
				.Where(f => f.IsPublic)
				.OrderBy(f => f.Index)
				.ToListAsync(cancellationToken);
		}

		if (_faceScope.IsPublicFace)
		{
			var query = BaseQuery();
			if (!string.IsNullOrEmpty(userId) && IsGlobalAdmin(user))
				return await query.OrderBy(f => f.Index).ToListAsync(cancellationToken);

			if (!string.IsNullOrEmpty(userId))
			{
				var privateFaceIds = await _context.UserFaceRoles
					.AsNoTracking()
					.Where(ufr => ufr.UserId == userId)
					.Select(ufr => ufr.FaceId)
					.ToListAsync(cancellationToken);

				return await query
					.Where(f => f.IsPublic || privateFaceIds.Contains(f.Id))
					.OrderBy(f => f.Index)
					.ToListAsync(cancellationToken);
			}

			return await query.Where(f => f.IsPublic).OrderBy(f => f.Index).ToListAsync(cancellationToken);
		}

		return await BaseQuery()
			.Where(f => f.Id == _faceScope.FaceId)
			.OrderBy(f => f.Index)
			.ToListAsync(cancellationToken);
	}

	private async Task<(Dictionary<int, (int RoleId, string RoleName)>? Roles, Dictionary<int, (bool Visited, bool FaceRoleIntroCompleted)>? State)>
		LoadUserFaceMetaAsync(string? userId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(userId))
			return (null, null);

		var userFaceRoles = await _context.UserFaceRoles
			.AsNoTracking()
			.Where(ufr => ufr.UserId == userId)
			.Include(ufr => ufr.UserRole)
			.ToListAsync(cancellationToken);

		var myFaceRoles = userFaceRoles
			.Where(ufr => ufr.UserRole != null)
			.ToDictionary(ufr => ufr.FaceId, ufr => (ufr.UserRoleId, ufr.UserRole!.Name));

		Dictionary<int, (bool Visited, bool FaceRoleIntroCompleted)>? myFaceState = null;
		var userProfileId = await _context.UserProfiles
			.AsNoTracking()
			.Where(up => up.UserId == userId)
			.Select(up => up.Id)
			.FirstOrDefaultAsync(cancellationToken);

		if (userProfileId != 0)
		{
			var ufRows = await _context.UserFaceProfiles
				.AsNoTracking()
				.Where(ufp => ufp.UserProfileId == userProfileId)
				.Select(ufp => new { ufp.FaceId, ufp.Visited, ufp.FaceRoleIntroCompleted })
				.ToListAsync(cancellationToken);
			myFaceState = ufRows.ToDictionary(x => x.FaceId, x => (x.Visited, x.FaceRoleIntroCompleted));
		}

		return (myFaceRoles, myFaceState);
	}
}
