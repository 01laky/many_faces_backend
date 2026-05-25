using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorPush;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests;

internal sealed class StaticPushSettingsProvider(OperatorPushSettingsValues values) : IOperatorPushSettingsProvider
{
	public Task<OperatorPushSettingsValues> GetAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(values);

	public Task<OperatorPushSettingsValues> SetAsync(OperatorPushSettingsValues values, CancellationToken cancellationToken = default) =>
		Task.FromResult(values);

	public Models.DTOs.Admin.AdminPushSettingsDto ToDto(OperatorPushSettingsValues values, PushOptions envOptions) =>
		throw new NotImplementedException();

	public void InvalidateCache()
	{
	}
}

/// <summary>
/// Ensures the push gRPC client is a safe no-op when operator push settings disallow sends.
/// </summary>
public sealed class PushWorkerGrpcClientTests
{
	[Fact]
	public async Task SendPushAsync_WhenDisabled_ReturnsNull()
	{
		var values = new OperatorPushSettingsValues(
			false,
			"http://localhost:59999",
			null,
			null,
			null,
			"push_test_title",
			"push_test_body",
			null,
			15,
			DateTime.UtcNow,
			null);
		var provider = new StaticPushSettingsProvider(values);
		var options = Options.Create(new PushOptions());
		using var sut = new PushWorkerGrpcClient(provider, options, NullLogger<PushWorkerGrpcClient>.Instance);
		var resp = await sut.SendPushAsync(new ManyFaces.Push.V1.SendPushRequest());
		Assert.Null(resp);
	}
}
