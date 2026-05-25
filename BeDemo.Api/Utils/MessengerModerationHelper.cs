using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Utils;

public static class MessengerModerationHelper
{
	public static async Task<HashSet<string>> GetSuperAdminUserIdsAsync(
		ApplicationDbContext context,
		IEnumerable<string> userIds,
		CancellationToken cancellationToken = default)
	{
		var ids = userIds.Distinct().ToList();
		if (ids.Count == 0)
			return new HashSet<string>(StringComparer.Ordinal);

		var superIds = await context.Users.AsNoTracking()
			.Where(u => ids.Contains(u.Id))
			.Join(
				context.UserRoles.AsNoTracking().Where(r => r.Name == UserRole.GlobalRoleNames.SuperAdmin),
				u => u.UserRoleId,
				r => r.Id,
				(u, _) => u.Id)
			.ToListAsync(cancellationToken);

		return superIds.ToHashSet(StringComparer.Ordinal);
	}

	public static bool ShouldHidePeerConversation(
		bool callerFaceBannedInScope,
		string otherUserId,
		HashSet<string> superAdminUserIds) =>
		callerFaceBannedInScope && !superAdminUserIds.Contains(otherUserId);
}
