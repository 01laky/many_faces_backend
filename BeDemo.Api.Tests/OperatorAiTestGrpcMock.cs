using BeDemo.Api.Services;
using BeDemo.Api.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BeDemo.Api.Tests;

/// <summary>
/// WebApplicationFactory that swaps the real AI gRPC client for the shared <see cref="FakeAiGrpcService"/> (registered
/// as both <see cref="IAiGrpcService"/> and the unguarded <see cref="IAiModelStatusClient"/> health-probe surface), and
/// shortens the enable-health polling windows so operator-AI activation tests run fast. Configure the fake via
/// <see cref="Ai"/> (e.g. <c>Ai.GenerateHandler</c>, <c>Ai.ModelStatusHandler</c>).
/// </summary>
public sealed class OperatorAiGrpcMockWebApplicationFactory : CustomWebApplicationFactory<Program>
{
	public FakeAiGrpcService Ai { get; } = new();

	protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.UseSetting("OperatorAi:EnableHealthLoadingWaitSeconds", "2");
		builder.UseSetting("OperatorAi:EnableHealthPollIntervalSeconds", "1");
		builder.ConfigureServices(services =>
		{
			services.RemoveAll<IAiModelStatusClient>();
			services.RemoveAll<IAiGrpcService>();
			services.AddSingleton<IAiModelStatusClient>(Ai);
			services.AddSingleton<IAiGrpcService>(Ai);
		});
	}
}
