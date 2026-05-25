using ManyFaces.Mailer.V1;

namespace BeDemo.Api.Services;

/// <summary>
/// Abstraction over <see cref="MailerService.MailerServiceClient"/> so tests can substitute fakes without a Java process.
/// </summary>
public interface IMailerWorkerClient
{
    /// <summary>
    /// Delivers one templated message through the worker. Returns null when mail is disabled or the channel is not built.
    /// </summary>
    Task<SendTemplatedEmailResponse?> SendTemplatedEmailAsync(
        SendTemplatedEmailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Opens SMTP transport without sending MIME.</summary>
    Task<TestSmtpConnectionResponse?> TestSmtpConnectionAsync(
        OperatorMail.OperatorMailSettingsValues settings,
        CancellationToken cancellationToken = default);
}
