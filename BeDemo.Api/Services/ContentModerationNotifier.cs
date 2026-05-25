using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>
/// EF-backed notifier: writes <see cref="Notification"/> entities so the existing user notification feed can surface moderation events.
/// </summary>
public sealed class ContentModerationNotifier : IContentModerationNotifier
{
	private readonly ApplicationDbContext _context;

	public ContentModerationNotifier(ApplicationDbContext context)
	{
		_context = context;
	}

	public void NotifyCreator(string creatorId, string title, string message, string type = "content_moderation")
	{
		// Defensive: anonymous or system paths should not create orphan rows.
		if (string.IsNullOrWhiteSpace(creatorId))
			return;
		_context.Notifications.Add(new Notification
		{
			UserId = creatorId,
			Title = title,
			Message = message,
			Type = type,
			CreatedAt = DateTime.UtcNow,
		});
	}

	public async Task NotifySuperAdminsAsync(
		string title,
		string message,
		string type = "moderation_ops",
		CancellationToken cancellationToken = default)
	{
		// Resolve super-admins by role name (global scope); each receives their own notification row.
		var ids = await _context.Users
			.AsNoTracking()
			.Where(u => u.UserRole.Name == UserRole.GlobalRoleNames.SuperAdmin)
			.Select(u => u.Id)
			.ToListAsync(cancellationToken);
		foreach (var id in ids)
		{
			_context.Notifications.Add(new Notification
			{
				UserId = id,
				Title = title,
				Message = message,
				Type = type,
				CreatedAt = DateTime.UtcNow,
			});
		}
	}
}
