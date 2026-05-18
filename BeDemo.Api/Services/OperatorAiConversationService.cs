using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Models.Requests.OperatorAi;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services;

public sealed class OperatorAiConversationService : IOperatorAiConversationService
{
    private readonly ApplicationDbContext _context;
    private readonly OperatorAiOptions _options;

    public OperatorAiConversationService(ApplicationDbContext context, IOptions<OperatorAiOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<OperatorAiConversationListItemDto>> ListConversationsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, _options.MaxConversationsListPageSize);
        return await ProjectListItems()
            .OrderByDescending(c => c.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<OperatorAiConversationListItemDto?> GetConversationAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        await ProjectListItems()
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<OperatorAiConversationListItemDto> CreateConversationAsync(
        string userId,
        CreateOperatorAiConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = new OperatorAiConversation
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _context.OperatorAiConversations.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await EnforceConversationRetentionAsync(cancellationToken);
        return (await GetConversationAsync(entity.Id, cancellationToken))!;
    }

    public async Task<OperatorAiConversationListItemDto?> UpdateConversationAsync(
        int id,
        UpdateOperatorAiConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.OperatorAiConversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity == null)
            return null;

        if (request.Title != null)
            entity.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return await GetConversationAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteConversationAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.OperatorAiConversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity == null)
            return false;

        _context.OperatorAiConversations.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<OperatorAiMessagesPageDto> GetMessagesPageAsync(
        int conversationId,
        OperatorAiMessagesQuery query,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(query.Limit, 1, _options.MessagesPageSize);
        var exists = await _context.OperatorAiConversations.AsNoTracking()
            .AnyAsync(c => c.Id == conversationId, cancellationToken);
        if (!exists)
            return new OperatorAiMessagesPageDto();

        IQueryable<OperatorAiMessage> q = _context.OperatorAiMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        if (query.BeforeId is { } beforeId)
            q = q.Where(m => m.Id < beforeId);

        var rows = await q
            .OrderByDescending(m => m.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        if (hasMore)
            rows = rows.Take(limit).ToList();

        rows.Reverse();
        var items = rows.Select(ToMessageDto).ToList();
        return new OperatorAiMessagesPageDto
        {
            Items = items,
            HasMore = hasMore,
            OldestId = items.Count > 0 ? items[0].Id : null,
        };
    }

    public async Task<(OperatorAiMessageDto User, OperatorAiMessageDto Assistant)> AppendExchangeAsync(
        int conversationId,
        string userId,
        string operatorEmail,
        string responseLocale,
        string userContent,
        string assistantContent,
        string? statsMode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operatorEmail))
            throw new ArgumentException("Operator email is required.", nameof(operatorEmail));
        if (!OperatorAiLocaleValidator.TryNormalize(responseLocale, out var normalizedLocale))
            throw new ArgumentException("Invalid response locale.", nameof(responseLocale));

        var conversation = await _context.OperatorAiConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation {conversationId} not found.");

        var now = DateTime.UtcNow;
        var userMsg = new OperatorAiMessage
        {
            ConversationId = conversationId,
            Role = OperatorAiMessage.RoleUser,
            Content = userContent,
            StatsMode = statsMode,
            CreatedByUserId = userId,
            AuthorEmail = operatorEmail.Trim(),
            ResponseLocale = normalizedLocale,
            CreatedAt = now,
        };
        var assistantMsg = new OperatorAiMessage
        {
            ConversationId = conversationId,
            Role = OperatorAiMessage.RoleAssistant,
            Content = assistantContent,
            ResponseLocale = normalizedLocale,
            CreatedAt = now.AddMilliseconds(1),
        };

        _context.OperatorAiMessages.Add(userMsg);
        _context.OperatorAiMessages.Add(assistantMsg);
        conversation.UpdatedAt = now;

        if (string.IsNullOrWhiteSpace(conversation.Title))
        {
            var preview = userContent.Trim();
            conversation.Title = preview.Length > 80 ? preview[..80] + "…" : preview;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (ToMessageDto(userMsg), ToMessageDto(assistantMsg));
    }

    public async Task<IReadOnlyList<ChatHistoryEntry>> GetRecentHistoryPairsAsync(
        int conversationId,
        int maxPairs,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Max(0, maxPairs) * 2;
        if (take == 0)
            return Array.Empty<ChatHistoryEntry>();

        var messages = await _context.OperatorAiMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        var pairs = new List<ChatHistoryEntry>();
        for (var i = 0; i + 1 < messages.Count; i += 2)
        {
            if (messages[i].Role == OperatorAiMessage.RoleUser &&
                messages[i + 1].Role == OperatorAiMessage.RoleAssistant)
            {
                pairs.Add(new ChatHistoryEntry
                {
                    UserMessage = messages[i].Content,
                    AiResponse = messages[i + 1].Content,
                });
            }
        }

        return pairs.Count <= maxPairs ? pairs : pairs.TakeLast(maxPairs).ToList();
    }

    public async Task EnforceConversationRetentionAsync(CancellationToken cancellationToken = default)
    {
        var max = _options.MaxConversations;
        var count = await _context.OperatorAiConversations.CountAsync(cancellationToken);
        if (count <= max)
            return;

        var toRemove = await _context.OperatorAiConversations
            .OrderBy(c => c.UpdatedAt)
            .Take(count - max)
            .ToListAsync(cancellationToken);
        _context.OperatorAiConversations.RemoveRange(toRemove);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<OperatorAiConversationListItemDto> ProjectListItems() =>
        from c in _context.OperatorAiConversations.AsNoTracking()
        join u in _context.Users.AsNoTracking() on c.CreatedByUserId equals u.Id into users
        from u in users.DefaultIfEmpty()
        select new OperatorAiConversationListItemDto
        {
            Id = c.Id,
            Title = c.Title,
            CreatedByUserId = c.CreatedByUserId,
            CreatedByDisplayName = u == null ? null : (u.UserName ?? u.Email),
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
        };

    private static OperatorAiMessageDto ToMessageDto(OperatorAiMessage m) =>
        new()
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            StatsMode = m.StatsMode,
            CreatedByUserId = m.CreatedByUserId,
            AuthorEmail = m.AuthorEmail,
            ResponseLocale = m.ResponseLocale,
            CreatedAt = m.CreatedAt,
        };
}
