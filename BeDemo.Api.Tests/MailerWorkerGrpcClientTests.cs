using BeDemo.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class MailerWorkerGrpcClientTests
{
    [Fact]
    public async Task SendTemplatedEmailAsync_WhenDisabled_ReturnsNull()
    {
        var options = Options.Create(new MailOptions { Enabled = false, WorkerGrpcUrl = "http://localhost:59998" });
        using var sut = new MailerWorkerGrpcClient(
            options,
            NullLogger<MailerWorkerGrpcClient>.Instance,
            new HttpContextAccessor());
        var resp = await sut.SendTemplatedEmailAsync(new ManyFaces.Mailer.V1.SendTemplatedEmailRequest());
        Assert.Null(resp);
    }
}
