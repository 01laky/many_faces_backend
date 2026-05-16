using BeDemo.Api.Services;
using ManyFaces.Mailer.V1;

namespace BeDemo.Api.Tests;

public sealed class CapturingMailerWorkerClient : IMailerWorkerClient
{
    public SendTemplatedEmailRequest? LastRequest { get; private set; }

    public void Reset() => LastRequest = null;

    public Task<SendTemplatedEmailResponse?> SendTemplatedEmailAsync(
        SendTemplatedEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult<SendTemplatedEmailResponse?>(new SendTemplatedEmailResponse { CorrelationId = "test-corr" });
    }

    public void Dispose()
    {
    }
}
