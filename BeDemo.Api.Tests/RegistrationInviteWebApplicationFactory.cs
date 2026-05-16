using BeDemo.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BeDemo.Api.Tests;

public sealed class RegistrationInviteWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    public CapturingMailerWorkerClient CapturingMailer { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Mail:Enabled", "true");
        builder.UseSetting("Mail:WorkerGrpcUrl", "http://localhost:59998");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMailerWorkerClient>();
            services.AddSingleton<IMailerWorkerClient>(CapturingMailer);
        });
        base.ConfigureWebHost(builder);
    }
}
