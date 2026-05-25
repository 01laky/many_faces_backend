using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Security;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services;

public class AccessCapabilitiesService : IAccessCapabilitiesService
{
	private readonly ApplicationDbContext _db;
	private readonly IFaceScopeContext _faceScope;

	public AccessCapabilitiesService(ApplicationDbContext db, IFaceScopeContext faceScope)
	{
		_db = db;
		_faceScope = faceScope;
	}

	public async Task<CapabilitiesResponse> GetCapabilitiesAsync(
		string userId,
		ClaimsPrincipal principal,
		CancellationToken cancellationToken = default)
	{
		var globalName = await _db.Users.AsNoTracking()
			.Where(u => u.Id == userId)
			.Join(_db.UserRoles.AsNoTracking(), u => u.UserRoleId, r => r.Id, (_, r) => r.Name)
			.FirstOrDefaultAsync(cancellationToken) ?? UserRole.GlobalRoleNames.User;

		string? faceRoleName = null;
		if (_faceScope.IsAvailable)
		{
			faceRoleName = await _db.UserFaceRoles.AsNoTracking()
				.Where(ufr => ufr.UserId == userId && ufr.FaceId == _faceScope.FaceId)
				.Join(_db.UserRoles.AsNoTracking(), ufr => ufr.UserRoleId, r => r.Id, (_, r) => r.Name)
				.FirstOrDefaultAsync(cancellationToken);
		}

		var permissions = new List<string>();
		if (PlatformAccessRules.IsGlobalSuperAdmin(principal))
			permissions.Add(AclPermissionKeys.PlatformSuper);

		if (PlatformAccessRules.CanManageAllFaces(_faceScope, principal))
			permissions.Add(AclPermissionKeys.PlatformAdmin);

		if (PlatformAccessRules.CanMutateGlobalPageTypes(_faceScope, principal))
			permissions.Add(AclPermissionKeys.PlatformPagetypeMutate);

		if (_faceScope.IsAvailable)
			permissions.Add(AclPermissionKeys.TenantSession);

		if (!string.IsNullOrEmpty(faceRoleName))
			permissions.Add(AclPermissionKeys.FaceMember);

		// Any authenticated user in a tenant may use the self-service picker; server still enforces whitelist on PUT.
		if (_faceScope.IsAvailable && !_faceScope.IsAdminFaceScope)
			permissions.Add(AclPermissionKeys.FaceRoleSelfService);

		return new CapabilitiesResponse
		{
			GlobalRole = globalName,
			RequestFaceId = _faceScope.IsAvailable ? _faceScope.FaceId : 0,
			RequestFaceIndex = _faceScope.IsAvailable ? _faceScope.FaceIndex : null,
			IsAdminFaceScope = _faceScope.IsAdminFaceScope,
			MyFaceRoleName = faceRoleName,
			Permissions = permissions.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToList(),
		};
	}
}
