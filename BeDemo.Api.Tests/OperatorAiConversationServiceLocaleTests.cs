using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiConversationServiceLocaleTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public OperatorAiConversationServiceLocaleTests(CustomWebApplicationFactory<Program> factory) =>
        _factory = factory;

    [Fact]
    public async Task AppendExchangeAsync_persists_author_email_and_response_locale()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = new OperatorAiConversationService(
            context,
            Options.Create(new OperatorAiOptions()));

        var user = await context.Users.AsNoTracking().FirstAsync();
        var conv = new OperatorAiConversation
        {
            Title = "Locale test",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.OperatorAiConversations.Add(conv);
        await context.SaveChangesAsync();

        var (userDto, assistantDto) = await service.AppendExchangeAsync(
            conv.Id,
            user.Id,
            user.Email ?? "op@test.com",
            "en",
            "How many users?",
            "There are 33 registered users.",
            "inline");

        userDto.AuthorEmail.Should().Be(user.Email);
        userDto.ResponseLocale.Should().Be("en");
        assistantDto.ResponseLocale.Should().Be("en");
        assistantDto.AuthorEmail.Should().BeNull();
    }
}
