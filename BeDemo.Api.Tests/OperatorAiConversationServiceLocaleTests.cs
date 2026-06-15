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

	[Fact]
	public async Task AppendExchangeAsync_persists_assistant_duration_only_on_the_assistant_row()
	{
		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var service = new OperatorAiConversationService(context, Options.Create(new OperatorAiOptions()));

		var user = await context.Users.AsNoTracking().FirstAsync();
		var conv = new OperatorAiConversation
		{
			Title = "Duration test",
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
			"How many reels are pending?",
			"Reels: 640 total — 256 pending, 384 approved.",
			"live",
			assistantDurationMs: 1234);

		// The server-measured duration lands on the assistant row only; the user row stays null.
		assistantDto.DurationMs.Should().Be(1234);
		userDto.DurationMs.Should().BeNull();

		// And it is actually persisted (not just mapped on the returned DTO).
		var stored = await context.OperatorAiMessages.AsNoTracking()
			.Where(m => m.ConversationId == conv.Id)
			.OrderBy(m => m.CreatedAt)
			.ToListAsync();
		stored.Single(m => m.Role == OperatorAiMessage.RoleAssistant).DurationMs.Should().Be(1234);
		stored.Single(m => m.Role == OperatorAiMessage.RoleUser).DurationMs.Should().BeNull();
	}

	[Fact]
	public async Task AppendExchangeAsync_null_or_zero_duration_stays_null()
	{
		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var service = new OperatorAiConversationService(context, Options.Create(new OperatorAiOptions()));

		var user = await context.Users.AsNoTracking().FirstAsync();
		var conv = new OperatorAiConversation
		{
			Title = "Duration null test",
			CreatedByUserId = user.Id,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
		};
		context.OperatorAiConversations.Add(conv);
		await context.SaveChangesAsync();

		// Default (no duration provided) and an explicit 0 both store null (treated as "unmeasured").
		var (_, defaultDto) = await service.AppendExchangeAsync(
			conv.Id, user.Id, user.Email ?? "op@test.com", "en", "q1", "a1", "live");
		defaultDto.DurationMs.Should().BeNull();

		var (_, zeroDto) = await service.AppendExchangeAsync(
			conv.Id, user.Id, user.Email ?? "op@test.com", "en", "q2", "a2", "live", assistantDurationMs: 0);
		zeroDto.DurationMs.Should().BeNull();
	}
}
