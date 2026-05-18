using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.OperatorUserChat;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>
/// REST read model for super-admin user chat: per-operator conversation list, paginated history, read receipts.
/// All queries scope to <see cref="Message.IsPlatformDirectMessage"/> dyads and exclude other super-admins as targets.
/// </summary>
public sealed class OperatorUserChatService : IOperatorUserChatService
{
    private const int MaxLimit = 100;
    private readonly ApplicationDbContext _context;
    private readonly IPlatformDirectMessageService _platformMessages;

    public OperatorUserChatService(ApplicationDbContext context, IPlatformDirectMessageService platformMessages)
    {
        _context = context;
        _platformMessages = platformMessages;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OperatorUserChatConversationDto>> ListConversationsAsync(
        string operatorUserId,
        CancellationToken cancellationToken = default)
    {
        // Include platform-flagged rows and pre-migration super-admin sends for this operator.
        var superAdminRole = UserRole.GlobalRoleNames.SuperAdmin;
        var messages = await (
            from m in _context.Messages.AsNoTracking()
            join s in _context.Users.AsNoTracking() on m.SenderId equals s.Id
            join r in _context.UserRoles.AsNoTracking() on s.UserRoleId equals r.Id
            where (m.SenderId == operatorUserId || m.ReceiverId == operatorUserId)
                  && (m.IsPlatformDirectMessage || (!m.IsMessageRequest && r.Name == superAdminRole))
                  && (!m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted)
            select m).ToListAsync(cancellationToken);

        var otherIds = messages
            .Select(m => m.SenderId == operatorUserId ? m.ReceiverId : m.SenderId)
            .Distinct()
            .ToList();

        var superTargets = await (
            from u in _context.Users.AsNoTracking()
            join role in _context.UserRoles.AsNoTracking() on u.UserRoleId equals role.Id
            where otherIds.Contains(u.Id) && role.Name == superAdminRole
            select u.Id).ToListAsync(cancellationToken);
        var superSet = superTargets.ToHashSet(StringComparer.Ordinal);

        // Drop threads whose counterparty is another super-admin (not valid user-chat targets).
        var grouped = messages
            .Where(m =>
            {
                var other = m.SenderId == operatorUserId ? m.ReceiverId : m.SenderId;
                return !superSet.Contains(other);
            })
            .GroupBy(m => m.SenderId == operatorUserId ? m.ReceiverId : m.SenderId)
            .ToList();

        var userIds = grouped.Select(g => g.Key).ToList();
        var users = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return grouped
            .Select(g =>
            {
                var last = g.OrderByDescending(m => m.SentAt).First();
                users.TryGetValue(g.Key, out var other);
                var displayName = other == null
                    ? g.Key
                    : ((other.FirstName ?? "") + " " + (other.LastName ?? "")).Trim();
                if (string.IsNullOrEmpty(displayName))
                    displayName = other?.Email ?? g.Key;
                return new OperatorUserChatConversationDto(
                    g.Key,
                    other?.Email ?? string.Empty,
                    displayName,
                    last.Content.Length > 120 ? last.Content[..120] + "…" : last.Content,
                    last.SentAt,
                    last.SenderId == operatorUserId,
                    g.Count(m => m.ReceiverId == operatorUserId && m.ReadAt == null));
            })
            .OrderByDescending(c => c.LastMessageAtUtc)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<OperatorUserChatHistoryPageDto?> GetHistoryAsync(
        string operatorUserId,
        string targetUserId,
        OperatorUserChatHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await IsValidTargetAsync(operatorUserId, targetUserId, cancellationToken))
            return null;

        var limit = Math.Clamp(query.Limit, 1, MaxLimit);
        var baseQuery = PlatformDyadQuery(operatorUserId, targetUserId);

        if (query.BeforeId is { } beforeId)
            baseQuery = baseQuery.Where(m => m.Id < beforeId);

        var rows = await baseQuery
            .Include(m => m.Sender).ThenInclude(u => u.UserRole)
            .OrderByDescending(m => m.SentAt)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        if (hasMore)
            rows = rows.Take(limit).ToList();

        rows.Reverse();

        var items = rows.Select(MapMessage).ToList();
        return new OperatorUserChatHistoryPageDto(items, hasMore);
    }

    /// <inheritdoc />
    public async Task<OperatorUserChatThreadExistsDto> GetThreadExistsAsync(
        string operatorUserId,
        string targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsValidTargetAsync(operatorUserId, targetUserId, cancellationToken))
            return new OperatorUserChatThreadExistsDto(false, 0);

        var exists = await _platformMessages.ThreadExistsAsync(operatorUserId, targetUserId, cancellationToken);
        if (!exists)
            return new OperatorUserChatThreadExistsDto(false, 0);

        var count = await PlatformDyadQuery(operatorUserId, targetUserId).CountAsync(cancellationToken);
        return new OperatorUserChatThreadExistsDto(true, count);
    }

    /// <inheritdoc />
    public async Task<int> MarkReadAsync(string operatorUserId, string targetUserId, CancellationToken cancellationToken = default)
    {
        var toUpdate = await PlatformDyadQuery(operatorUserId, targetUserId)
            .Where(m => m.SenderId == targetUserId && m.ReceiverId == operatorUserId && m.ReadAt == null)
            .ToListAsync(cancellationToken);

        foreach (var m in toUpdate)
            m.ReadAt = DateTime.UtcNow;

        if (toUpdate.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return toUpdate.Count;
    }

    /// <summary>Messages between one super-admin and one end user in the platform DM channel only.</summary>
    private IQueryable<Message> PlatformDyadQuery(string operatorUserId, string targetUserId) =>
        _context.Messages.Where(m =>
            ((m.SenderId == operatorUserId && m.ReceiverId == targetUserId)
             || (m.SenderId == targetUserId && m.ReceiverId == operatorUserId))
            && m.IsPlatformDirectMessage
            && (!m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted));

    private async Task<bool> IsValidTargetAsync(string operatorUserId, string targetUserId, CancellationToken cancellationToken)
    {
        if (string.Equals(operatorUserId, targetUserId, StringComparison.Ordinal))
            return false;

        var target = await _context.Users.AsNoTracking()
            .Include(u => u.UserRole)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (target == null)
            return false;

        return !OperatorModerationGuard.IsGlobalSuperAdminRole(target.UserRole?.Name);
    }

    private static OperatorUserChatMessageDto MapMessage(Message m)
    {
        var senderName = ((m.Sender.FirstName ?? "") + " " + (m.Sender.LastName ?? "")).Trim();
        if (string.IsNullOrEmpty(senderName))
            senderName = m.Sender.Email ?? m.SenderId;
        return new OperatorUserChatMessageDto(
            m.Id,
            m.SenderId,
            senderName,
            m.Sender.UserRole?.Name,
            OperatorModerationGuard.IsGlobalSuperAdminRole(m.Sender.UserRole?.Name),
            m.Content,
            m.SentAt,
            m.ReadAt);
    }
}
