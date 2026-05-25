using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorMail;
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
        var provider = new DisabledOperatorMailSettingsProvider();
        var options = Options.Create(new MailOptions { Enabled = false, WorkerGrpcUrl = "http://localhost:59998" });
        using var sut = new MailerWorkerGrpcClient(
            provider,
            options,
            NullLogger<MailerWorkerGrpcClient>.Instance,
            new HttpContextAccessor());
        var resp = await sut.SendTemplatedEmailAsync(new ManyFaces.Mailer.V1.SendTemplatedEmailRequest());
        Assert.Null(resp);
    }

    private sealed class DisabledOperatorMailSettingsProvider : IOperatorMailSettingsProvider
    {
        private static readonly OperatorMailSettingsValues Disabled = new(
            false,
            "en",
            "http://localhost:59998",
            null,
            "mailpit",
            1025,
            false,
            null,
            null,
            "no-reply@test.invalid",
            null,
            "http://localhost:9081",
            "/{locale}/register/complete",
            "manyfaces://register/complete",
            false,
            DateTime.UtcNow,
            null);

        public Task<OperatorMailSettingsValues> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Disabled);

        public Task<OperatorMailSettingsValues> SetAsync(OperatorMailSettingsValues values, CancellationToken cancellationToken = default) =>
            Task.FromResult(values);

        public Models.DTOs.Admin.AdminMailSettingsDto ToDto(OperatorMailSettingsValues values) =>
            new()
            {
                Enabled = values.Enabled,
                DefaultLocale = values.DefaultLocale,
                Smtp = new Models.DTOs.Admin.AdminMailSmtpSettingsDto
                {
                    Host = values.SmtpHost,
                    Port = values.SmtpPort,
                    StartTls = values.SmtpStartTls,
                    HasPassword = values.HasSmtpPassword,
                },
                From = new Models.DTOs.Admin.AdminMailFromSettingsDto { Email = values.FromEmail },
                RegistrationLinks = new Models.DTOs.Admin.AdminMailRegistrationLinksDto
                {
                    PortalPublicBaseUrl = values.PortalPublicBaseUrl,
                    CompleteRegistrationPathTemplate = values.CompleteRegistrationPathTemplate,
                    MobileDeepLinkBase = values.MobileDeepLinkBase,
                    PreferMobileDeepLinkWhenPlatformMobile = values.PreferMobileDeepLinkWhenPlatformMobile,
                },
                EffectiveStatus = values.EffectiveStatus,
                UpdatedAtUtc = values.UpdatedAtUtc,
            };

        public void InvalidateCache()
        {
        }
    }
}
