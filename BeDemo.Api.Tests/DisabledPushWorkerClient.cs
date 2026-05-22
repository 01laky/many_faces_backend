using BeDemo.Api.Services;
using ManyFaces.Push.V1;

namespace BeDemo.Api.Tests;

/// <summary>Simulates push worker disabled — <see cref="SendPushAsync"/> returns null.</summary>
public sealed class DisabledPushWorkerClient : IPushWorkerClient
{
    public Task<SendPushResponse?> SendPushAsync(
        SendPushRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<SendPushResponse?>(null);
}
