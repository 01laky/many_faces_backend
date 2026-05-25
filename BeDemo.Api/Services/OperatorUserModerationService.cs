using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.OperatorUsers;
using BeDemo.Api.Utils;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

public sealed class OperatorUserModerationService : IOperatorUserModerationService
{
	private static readonly DateTimeOffset GlobalBanLockoutEnd = DateTimeOffset.UtcNow.AddYears(100);

	private readonly ApplicationDbContext _context;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly IOAuthRefreshTokenStore _refreshTokens;
	private readonly IFaceModerationService _faceModeration;
	private readonly IPlatformDirectMessageService _platformDirectMessages;
	private readonly ILogger<OperatorUserModerationService> _logger;

	public OperatorUserModerationService(
		ApplicationDbContext context,
		UserManager<ApplicationUser> userManager,
		IOAuthRefreshTokenStore refreshTokens,
		IFaceModerationService faceModeration,
		IPlatformDirectMessageService platformDirectMessages,
		ILogger<OperatorUserModerationService> logger)
	{
		_context = context;
		_userManager = userManager;
		_refreshTokens = refreshTokens;
		_faceModeration = faceModeration;
		_platformDirectMessages = platformDirectMessages;
		_logger = logger;
	}

	public async Task<OperatorUserDetailDto?> GetDetailAsync(string targetUserId, CancellationToken cancellationToken = default)
	{
		var user = await _context.Users.AsNoTracking()
			.Include(u => u.UserRole)
			.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
		if (user == null)
			return null;

		var activeFaceBanCount = await _context.UserFaceModerations.AsNoTracking()
			.CountAsync(m => m.UserId == targetUserId && m.LiftedAt == null, cancellationToken);

		var faceRows = await (
			from ufr in _context.UserFaceRoles.AsNoTracking()
			join f in _context.Faces.AsNoTracking() on ufr.FaceId equals f.Id
			join r in _context.UserRoles.AsNoTracking() on ufr.UserRoleId equals r.Id
			where ufr.UserId == targetUserId
			select new
			{
				ufr.FaceId,
				f.Index,
				f.Title,
				ufr.UserRoleId,
				RoleName = r.Name,
			}).ToListAsync(cancellationToken);

		var bannedFaceIds = await _context.UserFaceModerations.AsNoTracking()
			.Where(m => m.UserId == targetUserId && m.LiftedAt == null)
			.Select(m => m.FaceId)
			.ToListAsync(cancellationToken);
		var bannedSet = bannedFaceIds.ToHashSet();

		var profileId = await _context.UserProfiles.AsNoTracking()
			.Where(up => up.UserId == targetUserId)
			.Select(up => (int?)up.Id)
			.FirstOrDefaultAsync(cancellationToken);

		var activeMap = profileId == null
			? new Dictionary<int, bool>()
			: await _context.UserFaceProfiles.AsNoTracking()
				.Where(ufp => ufp.UserProfileId == profileId.Value)
				.ToDictionaryAsync(ufp => ufp.FaceId, ufp => ufp.IsActive, cancellationToken);

		return new OperatorUserDetailDto
		{
			Id = user.Id,
			Email = user.Email,
			FirstName = user.FirstName,
			LastName = user.LastName,
			CreatedAt = user.CreatedAt,
			GlobalRole = new OperatorUserGlobalRoleDto
			{
				UserRoleId = user.UserRoleId,
				Name = user.UserRole.Name,
			},
			Badges = new OperatorUserBadgesDto
			{
				IsGloballyBanned = _faceModeration.IsUserGloballyBanned(user),
				ActiveFaceBanCount = activeFaceBanCount,
				EmailConfirmed = user.EmailConfirmed,
				AccessTokenVersion = user.AccessTokenVersion,
			},
			Faces = faceRows.Select(fr => new OperatorUserFaceRowDto
			{
				FaceId = fr.FaceId,
				FaceIndex = fr.Index,
				FaceTitle = fr.Title,
				UserRoleId = fr.UserRoleId,
				RoleName = fr.RoleName,
				IsActiveParticipant = activeMap.TryGetValue(fr.FaceId, out var active) && active,
				IsFaceBanned = bannedSet.Contains(fr.FaceId),
			}).OrderBy(f => f.FaceIndex).ToList(),
		};
	}

	public async Task<(bool Success, string? Error, int StatusCode)> SetFaceRoleAsync(
		string operatorUserId,
		string targetUserId,
		int faceId,
		int userRoleId,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		var target = await LoadTargetWithRoleAsync(targetUserId, cancellationToken);
		if (target == null)
			return (false, "User not found", StatusCodes.Status404NotFound);
		if (!OperatorModerationGuard.CanChangeFaceRole(target))
			return (false, "Cannot change role for this user", StatusCodes.Status403Forbidden);

		return await ApplyFaceRoleChangeAsync(operatorUserId, targetUserId, faceId, userRoleId, correlationId, cancellationToken);
	}

	public async Task<(bool Success, string? Error, int StatusCode)> SetSelfFaceRoleAsync(
		string userId,
		int faceId,
		int userRoleId,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		var hasMembership = await _context.UserFaceRoles.AsNoTracking()
			.AnyAsync(ufr => ufr.UserId == userId && ufr.FaceId == faceId, cancellationToken);
		if (!hasMembership)
			return (false, "Face membership not found", StatusCodes.Status404NotFound);

		return await ApplyFaceRoleChangeAsync(userId, userId, faceId, userRoleId, correlationId, cancellationToken);
	}

	private async Task<(bool Success, string? Error, int StatusCode)> ApplyFaceRoleChangeAsync(
		string operatorUserId,
		string targetUserId,
		int faceId,
		int userRoleId,
		string correlationId,
		CancellationToken cancellationToken)
	{
		var target = await LoadTargetWithRoleAsync(targetUserId, cancellationToken);
		if (target == null)
			return (false, "User not found", StatusCodes.Status404NotFound);

		var face = await _context.Faces.FindAsync(new object[] { faceId }, cancellationToken);
		if (face == null)
			return (false, "Face not found", StatusCodes.Status404NotFound);

		var role = await _context.UserRoles.FindAsync(new object[] { userRoleId }, cancellationToken);
		if (role == null || role.Scope != RoleScope.Face)
			return (false, "Invalid face role", StatusCodes.Status400BadRequest);

		var existing = await _context.UserFaceRoles
			.FirstOrDefaultAsync(ufr => ufr.UserId == targetUserId && ufr.FaceId == faceId, cancellationToken);
		string? previousRoleName = null;
		if (existing != null)
		{
			previousRoleName = await _context.UserRoles.AsNoTracking()
				.Where(r => r.Id == existing.UserRoleId)
				.Select(r => r.Name)
				.FirstOrDefaultAsync(cancellationToken);
			existing.UserRoleId = userRoleId;
		}
		else
		{
			_context.UserFaceRoles.Add(new UserFaceRole
			{
				UserId = targetUserId,
				FaceId = faceId,
				UserRoleId = userRoleId,
				CreatedAt = DateTime.UtcNow,
			});
		}

		var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == targetUserId, cancellationToken);
		if (userProfile != null)
		{
			var ufp = await _context.UserFaceProfiles
				.FirstOrDefaultAsync(x => x.UserProfileId == userProfile.Id && x.FaceId == faceId, cancellationToken);
			var isActive = FaceRoleParticipation.IsActiveForFaceRoleName(role.Name);
			if (ufp != null)
			{
				ufp.IsActive = isActive;
				ufp.FaceRoleIntroCompleted = true;
				ufp.UpdatedAt = DateTime.UtcNow;
			}
			else
			{
				_context.UserFaceProfiles.Add(new UserFaceProfile
				{
					UserProfileId = userProfile.Id,
					FaceId = faceId,
					IsActive = isActive,
					Visited = false,
					FaceRoleIntroCompleted = true,
					CreatedAt = DateTime.UtcNow,
				});
			}
		}

		await _context.SaveChangesAsync(cancellationToken);
		SecurityAuditLog.OperatorFaceRoleChanged(_logger, operatorUserId, targetUserId, faceId, previousRoleName, role.Name, correlationId);
		return (true, null, StatusCodes.Status200OK);
	}

	public async Task<(bool Success, string? Error, int StatusCode, bool AlreadyBanned)> GlobalBanAsync(
		string operatorUserId,
		string targetUserId,
		string reason,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		var target = await LoadTargetWithRoleAsync(targetUserId, cancellationToken);
		if (target == null)
			return (false, "User not found", StatusCodes.Status404NotFound, false);
		if (!OperatorModerationGuard.CanBanTarget(operatorUserId, target))
			return (false, "Cannot ban this user", StatusCodes.Status403Forbidden, false);

		if (_faceModeration.IsUserGloballyBanned(target))
			return (true, null, StatusCodes.Status200OK, true);

		var managed = await _userManager.FindByIdAsync(targetUserId);
		if (managed == null)
			return (false, "User not found", StatusCodes.Status404NotFound, false);

		await _userManager.SetLockoutEnabledAsync(managed, true);
		await _userManager.SetLockoutEndDateAsync(managed, GlobalBanLockoutEnd);
		managed.AccessTokenVersion++;
		var updateResult = await _userManager.UpdateAsync(managed);
		if (!updateResult.Succeeded)
			return (false, "Failed to apply global ban", StatusCodes.Status500InternalServerError, false);
		await _refreshTokens.RevokeAllActiveForUserAsync(targetUserId, cancellationToken);
		SecurityAuditLog.OperatorGlobalBan(_logger, operatorUserId, targetUserId, reason.Length, correlationId);
		return (true, null, StatusCodes.Status200OK, false);
	}

	public async Task<(bool Success, string? Error, int StatusCode)> GlobalUnbanAsync(
		string operatorUserId,
		string targetUserId,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		var target = await _userManager.FindByIdAsync(targetUserId);
		if (target == null)
			return (false, "User not found", StatusCodes.Status404NotFound);

		if (!_faceModeration.IsUserGloballyBanned(target))
			return (true, null, StatusCodes.Status204NoContent);

		await _userManager.SetLockoutEndDateAsync(target, null);
		await _userManager.SetLockoutEnabledAsync(target, false);
		var updateResult = await _userManager.UpdateAsync(target);
		if (!updateResult.Succeeded)
			return (false, "Failed to remove global ban", StatusCodes.Status500InternalServerError);
		SecurityAuditLog.OperatorGlobalUnban(_logger, operatorUserId, targetUserId, correlationId);
		return (true, null, StatusCodes.Status200OK);
	}

	public async Task<(bool Success, string? Error, int StatusCode, bool AlreadyBanned)> FaceBanAsync(
		string operatorUserId,
		string targetUserId,
		int faceId,
		string reason,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		var target = await LoadTargetWithRoleAsync(targetUserId, cancellationToken);
		if (target == null)
			return (false, "User not found", StatusCodes.Status404NotFound, false);
		if (!OperatorModerationGuard.CanBanTarget(operatorUserId, target))
			return (false, "Cannot ban this user", StatusCodes.Status403Forbidden, false);

		if (!await _context.Faces.AnyAsync(f => f.Id == faceId, cancellationToken))
			return (false, "Face not found", StatusCodes.Status404NotFound, false);

		var active = await _context.UserFaceModerations
			.FirstOrDefaultAsync(m => m.UserId == targetUserId && m.FaceId == faceId && m.LiftedAt == null, cancellationToken);
		if (active != null)
			return (true, null, StatusCodes.Status200OK, true);

		_context.UserFaceModerations.Add(new UserFaceModeration
		{
			UserId = targetUserId,
			FaceId = faceId,
			BannedByUserId = operatorUserId,
			Reason = reason.Trim(),
			BannedAt = DateTime.UtcNow,
		});
		await _context.SaveChangesAsync(cancellationToken);
		SecurityAuditLog.OperatorFaceBan(_logger, operatorUserId, targetUserId, faceId, reason.Length, correlationId);
		return (true, null, StatusCodes.Status200OK, false);
	}

	public async Task<(bool Success, string? Error, int StatusCode)> FaceUnbanAsync(
		string operatorUserId,
		string targetUserId,
		int faceId,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		var active = await _context.UserFaceModerations
			.FirstOrDefaultAsync(m => m.UserId == targetUserId && m.FaceId == faceId && m.LiftedAt == null, cancellationToken);
		if (active == null)
			return (true, null, StatusCodes.Status204NoContent);

		active.LiftedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);
		SecurityAuditLog.OperatorFaceUnban(_logger, operatorUserId, targetUserId, faceId, correlationId);
		return (true, null, StatusCodes.Status200OK);
	}

	public async Task<(bool Success, string? Error, int StatusCode, int? MessageId)> SendPlatformMessageAsync(
		string operatorUserId,
		string targetUserId,
		string content,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		var (hubError, messageId) = await _platformDirectMessages.SendAsync(
			operatorUserId,
			targetUserId,
			content,
			cancellationToken);

		if (hubError == OperatorUserChatHubErrorCodes.CannotMessageSelf)
			return (false, "Cannot message yourself", StatusCodes.Status400BadRequest, null);
		if (hubError == OperatorUserChatHubErrorCodes.TargetNotFound)
			return (false, "User not found", StatusCodes.Status404NotFound, null);
		if (hubError == OperatorUserChatHubErrorCodes.NotSuperAdmin)
			return (false, "Only super-admins can send platform messages", StatusCodes.Status403Forbidden, null);
		if (hubError == OperatorUserChatHubErrorCodes.CannotMessageSuperAdmin)
			return (false, "Cannot message a super-admin", StatusCodes.Status400BadRequest, null);
		if (hubError == OperatorUserChatHubErrorCodes.MessageTooLong)
			return (false, "Message too long", StatusCodes.Status400BadRequest, null);
		if (hubError == OperatorUserChatHubErrorCodes.EmptyContent)
			return (false, "Content is required", StatusCodes.Status400BadRequest, null);
		if (hubError != null)
			return (false, hubError, StatusCodes.Status400BadRequest, null);

		SecurityAuditLog.OperatorPlatformMessage(_logger, operatorUserId, targetUserId, content.Trim().Length, correlationId);
		return (true, null, StatusCodes.Status200OK, messageId);
	}

	private async Task<ApplicationUser?> LoadTargetWithRoleAsync(string targetUserId, CancellationToken cancellationToken) =>
		await _context.Users.Include(u => u.UserRole).FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
}
