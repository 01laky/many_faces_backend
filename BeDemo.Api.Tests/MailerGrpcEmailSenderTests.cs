using BeDemo.Api.Models;
using BeDemo.Api.Services;
using ManyFaces.Mailer.V1;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Globalization;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class MailerGrpcEmailSenderTests
{
    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        return new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Enumerable.Empty<IUserValidator<ApplicationUser>>(),
            Enumerable.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Mock.Of<IServiceProvider>(),
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    [Fact]
    public async Task SendEmailAsync_WhenMailDisabled_DoesNotCallWorker()
    {
        var client = new CapturingMailerWorkerClient();
        var userManager = CreateUserManagerMock();
        var options = Options.Create(new MailOptions { Enabled = false, WorkerGrpcUrl = "http://localhost:59998" });
        var sut = new MailerGrpcEmailSender(client, options, userManager.Object, NullLogger<MailerGrpcEmailSender>.Instance);

        var html =
            "<p>Please <a href=\"https://app.example/Account/ConfirmEmail?x=1\">confirm</a></p>";
        await sut.SendEmailAsync("user@example.com", "Confirm", html);

        Assert.Null(client.LastRequest);
    }

    [Fact]
    public async Task SendEmailAsync_ConfirmEmail_maps_to_identity_email_confirm_and_params()
    {
        var client = new CapturingMailerWorkerClient();
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.FindByEmailAsync("user@example.com")).ReturnsAsync(new ApplicationUser
        {
            Email = "user@example.com",
            NormalizedEmail = "USER@EXAMPLE.COM",
            UserName = "alice",
        });
        var options = Options.Create(new MailOptions { Enabled = true, WorkerGrpcUrl = "http://localhost:59998", DefaultLocale = "sk" });
        var sut = new MailerGrpcEmailSender(client, options, userManager.Object, NullLogger<MailerGrpcEmailSender>.Instance);

        var html =
            "<html><body><a href=\"https://app.example/Account/ConfirmEmail?userId=1&amp;code=secret\">link</a></body></html>";
        var prev = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("sk");
        try
        {
            await sut.SendEmailAsync("user@example.com", "Confirm your email", html);
        }
        finally
        {
            CultureInfo.CurrentUICulture = prev;
        }

        Assert.NotNull(client.LastRequest);
        Assert.Equal("identity_email_confirm", client.LastRequest!.TemplateId);
        Assert.Equal("sk", client.LastRequest.Locale);
        Assert.Equal("user@example.com", Assert.Single(client.LastRequest.To));
        Assert.StartsWith("https://app.example/Account/ConfirmEmail", client.LastRequest.Params["action_link"], StringComparison.Ordinal);
        Assert.Equal("alice", client.LastRequest.Params["user_name"]);
    }

    [Fact]
    public async Task SendEmailAsync_ResetPassword_maps_to_identity_password_reset()
    {
        var client = new CapturingMailerWorkerClient();
        var userManager = CreateUserManagerMock();
        userManager.Setup(m => m.FindByEmailAsync("u@example.com")).ReturnsAsync((ApplicationUser?)null);
        var options = Options.Create(new MailOptions { Enabled = true, WorkerGrpcUrl = "http://localhost:59998", DefaultLocale = "en" });
        var sut = new MailerGrpcEmailSender(client, options, userManager.Object, NullLogger<MailerGrpcEmailSender>.Instance);

        var html = "<p><a href=\"https://app.example/Account/ResetPassword?code=abc\">Reset</a></p>";
        await sut.SendEmailAsync("u@example.com", "Reset", html);

        Assert.NotNull(client.LastRequest);
        Assert.Equal("identity_password_reset", client.LastRequest!.TemplateId);
        Assert.Equal("u", client.LastRequest.Params["user_name"]);
    }

    [Fact]
    public async Task SendEmailAsync_WhenTemplateUnknown_does_not_call_worker()
    {
        var client = new CapturingMailerWorkerClient();
        var userManager = CreateUserManagerMock();
        var options = Options.Create(new MailOptions { Enabled = true, WorkerGrpcUrl = "http://localhost:59998" });
        var sut = new MailerGrpcEmailSender(client, options, userManager.Object, NullLogger<MailerGrpcEmailSender>.Instance);

        await sut.SendEmailAsync("user@example.com", "Hello", "<p>No markers here</p>");

        Assert.Null(client.LastRequest);
    }

    private sealed class CapturingMailerWorkerClient : IMailerWorkerClient
    {
        public SendTemplatedEmailRequest? LastRequest { get; private set; }

        public Task<SendTemplatedEmailResponse?> SendTemplatedEmailAsync(
            SendTemplatedEmailRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult<SendTemplatedEmailResponse?>(new SendTemplatedEmailResponse { CorrelationId = "test-corr" });
        }
    }
}
