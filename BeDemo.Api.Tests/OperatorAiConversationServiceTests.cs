using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.OperatorAi;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiConversationServiceTests
{
    private static OperatorAiConversationService CreateService(
        ApplicationDbContext context,
        OperatorAiOptions? options = null)
    {
        options ??= new OperatorAiOptions { MaxConversations = 1000, MessagesPageSize = 40, MaxHistoryPairs = 5 };
        return new OperatorAiConversationService(context, Options.Create(options));
    }

    private static ApplicationDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"operator_ai_{Guid.NewGuid():N}")
            .Options;
        var ctx = new ApplicationDbContext(opts);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task GetMessagesPage_returns_oldest_first_with_hasMore()
    {
        await using var ctx = CreateContext();
        var conv = new OperatorAiConversation
        {
            CreatedByUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ctx.OperatorAiConversations.Add(conv);
        await ctx.SaveChangesAsync();

        for (var i = 1; i <= 5; i++)
        {
            ctx.OperatorAiMessages.Add(new OperatorAiMessage
            {
                ConversationId = conv.Id,
                Role = OperatorAiMessage.RoleUser,
                Content = $"u{i}",
                CreatedAt = DateTime.UtcNow.AddSeconds(i),
            });
        }

        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, new OperatorAiOptions { MessagesPageSize = 2 });
        var page = await svc.GetMessagesPageAsync(conv.Id, new OperatorAiMessagesQuery { Limit = 2 });

        page.Items.Should().HaveCount(2);
        page.HasMore.Should().BeTrue();
        page.OldestId.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRecentHistoryPairs_returns_last_pairs_only()
    {
        await using var ctx = CreateContext();
        var conv = new OperatorAiConversation
        {
            CreatedByUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ctx.OperatorAiConversations.Add(conv);
        await ctx.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            ctx.OperatorAiMessages.Add(new OperatorAiMessage
            {
                ConversationId = conv.Id,
                Role = OperatorAiMessage.RoleUser,
                Content = $"u{i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(i * 2),
            });
            ctx.OperatorAiMessages.Add(new OperatorAiMessage
            {
                ConversationId = conv.Id,
                Role = OperatorAiMessage.RoleAssistant,
                Content = $"a{i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(i * 2 + 1),
            });
        }

        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, new OperatorAiOptions { MaxHistoryPairs = 2 });
        var history = await svc.GetRecentHistoryPairsAsync(conv.Id, 2);

        history.Should().HaveCount(2);
        history[0].UserMessage.Should().Be("u1");
        history[1].UserMessage.Should().Be("u2");
    }

    [Fact]
    public async Task EnforceConversationRetention_trims_oldest_by_updatedAt()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx, new OperatorAiOptions { MaxConversations = 2 });
        var now = DateTime.UtcNow;
        ctx.OperatorAiConversations.AddRange(
            new OperatorAiConversation
            {
                Title = "a",
                CreatedByUserId = "u1",
                CreatedAt = now,
                UpdatedAt = now,
            },
            new OperatorAiConversation
            {
                Title = "b",
                CreatedByUserId = "u1",
                CreatedAt = now,
                UpdatedAt = now.AddMinutes(1),
            },
            new OperatorAiConversation
            {
                Title = "c",
                CreatedByUserId = "u1",
                CreatedAt = now,
                UpdatedAt = now.AddMinutes(2),
            });
        await ctx.SaveChangesAsync();

        await svc.EnforceConversationRetentionAsync();

        var list = await svc.ListConversationsAsync(10);
        list.Should().HaveCount(2);
        list.Select(x => x.Title).Should().NotContain("a");
    }
}
