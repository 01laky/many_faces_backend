using BeDemo.Api.Models;
using BeDemo.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BeDemo.Api.Tests;

public sealed class CapturingOperatorAiGrpcService : IAiGrpcService
{
    public string? LastResponseLocale { get; private set; }
    public string? LastPrompt { get; private set; }

    public Func<string?, string>? GenerateHandler { get; set; }

    public Task<string> GenerateAsync(
        string prompt,
        int maxNewTokens = 50,
        string? statsContextJson = null,
        string? responseLocale = null,
        CancellationToken cancellationToken = default)
    {
        LastPrompt = prompt;
        LastResponseLocale = responseLocale;
        var text = GenerateHandler?.Invoke(responseLocale) ?? "...";
        return Task.FromResult(text);
    }

    public Task<string> OperatorStatsChatAsync(
        string userMessage,
        string historyText,
        bool fetchLivePublicSnapshot,
        string publicStatsAbsoluteUrl,
        int maxNewTokens = 150,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);

    public Task<AiContentReviewResult> ReviewContentAsync(
        AiContentReviewRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AiContentReviewResult(null, "not used"));

    public Task<AiModelStatus> GetModelStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AiModelStatus(true, false, false, "test-model"));
}

public sealed class OperatorAiGrpcMockWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public CapturingOperatorAiGrpcService Ai { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiGrpcService>();
            services.AddSingleton<IAiGrpcService>(Ai);
        });
    }
}
