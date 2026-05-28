using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Messenger;

public interface IConversationListService
{
	Task<ConversationListResult> GetConversationsAsync(
		string userId,
		int page,
		int pageSize,
		CancellationToken cancellationToken = default);
}

public sealed record ConversationListItem(
	string OtherUserId,
	string OtherUserName,
	string? OtherUserEmail,
	string LastMessage,
	DateTime LastMessageAt,
	bool LastMessageFromMe,
	int UnreadCount);

public sealed record ConversationListResult(
	IReadOnlyList<ConversationListItem> Items,
	int Page,
	int PageSize,
	int TotalCount,
	int TotalPages);

public sealed class ConversationListService : IConversationListService
{
	private readonly ApplicationDbContext _context;
	private readonly IFaceModerationService _faceModeration;
	private readonly IFaceScopeContext _faceScope;
	private readonly IOptions<PerformanceOptions> _perfOptions;

	public ConversationListService(
		ApplicationDbContext context,
		IFaceModerationService faceModeration,
		IFaceScopeContext faceScope,
		IOptions<PerformanceOptions> perfOptions)
	{
		_context = context;
		_faceModeration = faceModeration;
		_faceScope = faceScope;
		_perfOptions = perfOptions;
	}

	public async Task<ConversationListResult> GetConversationsAsync(
		string userId,
		int page,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		pageSize = Math.Clamp(pageSize, 1, 100);
		page = Math.Max(1, page);

		var blockedIds = await _context.UserBlocks
			.AsNoTracking()
			.Where(b => b.BlockerId == userId || b.BlockedId == userId)
			.Select(b => b.BlockerId == userId ? b.BlockedId : b.BlockerId)
			.ToListAsync(cancellationToken);

		var peerQuery = _context.Messages
			.AsNoTracking()
			.TagIfEnabled(_perfOptions, EfQueryTags.Conversations)
			.Where(m => m.SenderId == userId || m.ReceiverId == userId)
			.Where(m => !m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted)
			.Where(m => !blockedIds.Contains(m.SenderId == userId ? m.ReceiverId : m.SenderId))
			.Select(m => new
			{
				OtherUserId = m.SenderId == userId ? m.ReceiverId : m.SenderId,
				m.SentAt,
				m.Content,
				m.SenderId,
				m.ReceiverId,
				m.ReadAt,
			});

		var grouped = peerQuery
			.GroupBy(x => x.OtherUserId)
			.Select(g => new
			{
				OtherUserId = g.Key,
				LastMessageAt = g.Max(x => x.SentAt),
				UnreadCount = g.Count(x => x.ReceiverId == userId && x.ReadAt == null),
			});

		var totalCount = await grouped.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var pagePeers = await grouped
			.OrderByDescending(x => x.LastMessageAt)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		if (pagePeers.Count == 0)
			return new ConversationListResult(Array.Empty<ConversationListItem>(), page, pageSize, totalCount, totalPages);

		var peerIds = pagePeers.Select(p => p.OtherUserId).ToList();
		var users = await _context.Users
			.AsNoTracking()
			.Where(u => peerIds.Contains(u.Id))
			.Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
			.ToDictionaryAsync(u => u.Id, cancellationToken);

		var lastMessages = await _context.Messages
			.AsNoTracking()
			.Where(m =>
				(m.SenderId == userId && peerIds.Contains(m.ReceiverId)) ||
				(m.ReceiverId == userId && peerIds.Contains(m.SenderId)))
			.Where(m => !m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted)
			.GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
			.Select(g => new
			{
				OtherUserId = g.Key,
				Last = g.OrderByDescending(m => m.SentAt).Select(m => new { m.Content, m.SentAt, m.SenderId }).First(),
			})
			.ToDictionaryAsync(x => x.OtherUserId, x => x.Last, cancellationToken);

		var callerFaceBanned = _faceScope.IsAvailable &&
			await _faceModeration.ShouldBlockPeerActivityInFaceAsync(userId, _faceScope.FaceId, cancellationToken);
		var superAdminIds = await MessengerModerationHelper.GetSuperAdminUserIdsAsync(
			_context,
			peerIds,
			cancellationToken);

		var items = new List<ConversationListItem>();
		foreach (var peer in pagePeers)
		{
			if (MessengerModerationHelper.ShouldHidePeerConversation(callerFaceBanned, peer.OtherUserId, superAdminIds))
				continue;

			users.TryGetValue(peer.OtherUserId, out var other);
			lastMessages.TryGetValue(peer.OtherUserId, out var last);
			var name = other is null ? "" : $"{other.FirstName ?? ""} {other.LastName ?? ""}".Trim();
			items.Add(new ConversationListItem(
				peer.OtherUserId,
				name,
				other?.Email,
				last?.Content ?? "",
				peer.LastMessageAt,
				last?.SenderId == userId,
				peer.UnreadCount));
		}

		return new ConversationListResult(items, page, pageSize, totalCount, totalPages);
	}
}
