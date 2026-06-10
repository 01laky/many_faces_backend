using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Locale propagation acceptance (deterministic, no live Ollama). SignalR arity covered by <see cref="OperatorAiChatHubContractTests"/>.
/// </summary>
public sealed class OperatorAiLocaleAcceptanceTests : IClassFixture<OperatorAiGrpcMockWebApplicationFactory>
{
	private readonly OperatorAiGrpcMockWebApplicationFactory _factory;

	public OperatorAiLocaleAcceptanceTests(OperatorAiGrpcMockWebApplicationFactory factory) =>
		_factory = factory;

	[Fact]
	public async Task GenerateAsync_receives_en_sk_cz_locales_from_mock()
	{
		_factory.Ai.GenerateHandler = (_, locale) => locale switch
		{
			"en" => "There are 33 registered users in the system.",
			"sk" => "V systéme je registrovaných 33 používateľov.",
			"cz" => "V systému je registrováno 33 uživatelů.",
			_ => "...",
		};

		using var scope = _factory.Services.CreateScope();
		var grpc = scope.ServiceProvider.GetRequiredService<IAiGrpcService>();

		var en = await grpc.GenerateAsync("User: How many users?\nAI:", 50, null, "en");
		en.Should().Contain("33 registered users");
		_factory.Ai.LastResponseLocale.Should().Be("en");

		var sk = await grpc.GenerateAsync("User: Koľko používateľov?\nAI:", 50, null, "sk");
		sk.Should().Contain("33 používateľov");
		_factory.Ai.LastResponseLocale.Should().Be("sk");

		var cz = await grpc.GenerateAsync("User: Kolik uzivatelu?\nAI:", 50, null, "cz");
		cz.Should().Contain("33 uživatelů");
	}

	[Fact]
	public async Task After_sk_history_new_en_exchange_persists_en_assistant_locale()
	{
		_factory.Ai.GenerateHandler = (_, locale) =>
			locale == "en"
				? "There are 33 registered users in the system."
				: "V systéme je registrovaných 33 používateľov.";

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var convService = scope.ServiceProvider.GetRequiredService<IOperatorAiConversationService>();
		var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email != null);

		var conv = new OperatorAiConversation
		{
			Title = "SK history",
			CreatedByUserId = user.Id,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
		};
		db.OperatorAiConversations.Add(conv);
		await db.SaveChangesAsync();

		await convService.AppendExchangeAsync(
			conv.Id,
			user.Id,
			user.Email!,
			"sk",
			"Koľko máme používateľov?",
			"V systéme je registrovaných 33 používateľov.",
			"off");

		var grpc = scope.ServiceProvider.GetRequiredService<IAiGrpcService>();
		var aiText = await grpc.GenerateAsync(
			"User: How many users do we have?\nAI:",
			50,
			null,
			"en");
		aiText.Should().Contain("33 registered users");

		var (_, assistant) = await convService.AppendExchangeAsync(
			conv.Id,
			user.Id,
			user.Email!,
			"en",
			"How many users do we have registered in the system?",
			aiText,
			"off");

		assistant.ResponseLocale.Should().Be("en");
		assistant.Content.Should().Contain("33 registered users");

		var page = await convService.GetMessagesPageAsync(
			conv.Id,
			new BeDemo.Api.Models.Requests.OperatorAi.OperatorAiMessagesQuery { Limit = 50 });
		page.Items.Count(m => m.Role == OperatorAiMessage.RoleAssistant).Should().Be(2);
		page.Items.Where(m => m.Role == OperatorAiMessage.RoleAssistant).Last().ResponseLocale.Should().Be("en");
	}

	[Fact]
	public void Invalid_locale_fails_validation_before_grpc()
	{
		OperatorAiLocaleValidator.TryNormalize("de", out _).Should().BeFalse();
		OperatorAiHubErrorCodes.InvalidLocale.Should().Be("invalid_locale");
	}
}
