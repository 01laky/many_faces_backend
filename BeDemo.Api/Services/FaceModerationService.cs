using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

public sealed class FaceModerationService : IFaceModerationService
{
	private readonly ApplicationDbContext _context;

	public FaceModerationService(ApplicationDbContext context) => _context = context;

	public async Task<bool> IsUserBannedFromFaceAsync(string userId, int faceId, CancellationToken cancellationToken = default) =>
		await _context.UserFaceModerations.AsNoTracking()
			.AnyAsync(
				m => m.UserId == userId && m.FaceId == faceId && m.LiftedAt == null,
				cancellationToken);

	public bool IsUserGloballyBanned(ApplicationUser user) =>
		user.LockoutEnabled
		&& user.LockoutEnd.HasValue
		&& user.LockoutEnd > DateTimeOffset.UtcNow;

	public Task<bool> ShouldBlockPeerActivityInFaceAsync(string userId, int faceId, CancellationToken cancellationToken = default) =>
		IsUserBannedFromFaceAsync(userId, faceId, cancellationToken);
}
