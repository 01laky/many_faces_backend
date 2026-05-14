using System.Globalization;
using BeDemo.Api.Models;
using ManyFaces.Mailer.V1;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// Identity-facing email entry point that forwards to many_faces_mailer over gRPC.
/// </summary>
/// <remarks>
/// Pattern A (architecture doc): we never forward Identity's pre-rendered <c>htmlMessage</c> as the MIME body.
/// Instead we classify the flow (confirm vs reset) from markers still present in the HTML fragment Identity passes,
/// extract the single callback URL Identity already embedded (so we reuse the same confirmation token the UI generated),
/// and let the Java worker render localized templates from <c>template_id</c> + params.
/// If classification fails, we log and skip sending — better than double-sending arbitrary HTML through the worker.
/// </remarks>
public sealed class MailerGrpcEmailSender : IEmailSender
{
    private readonly IMailerWorkerClient _mailerWorkerClient;
    private readonly IOptions<MailOptions> _mailOptions;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<MailerGrpcEmailSender> _logger;

    public MailerGrpcEmailSender(
        IMailerWorkerClient mailerWorkerClient,
        IOptions<MailOptions> mailOptions,
        UserManager<ApplicationUser> userManager,
        ILogger<MailerGrpcEmailSender> logger)
    {
        _mailerWorkerClient = mailerWorkerClient;
        _mailOptions = mailOptions;
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var options = _mailOptions.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation(
                "Mail:Enabled is false; skipping outbound mail to {Recipient} (subject: {Subject}). Configure Mail:* and many_faces_mailer to enable.",
                RedactEmail(email),
                subject);
            return;
        }

        var templateId = MailerIdentityEmailFlowClassifier.ClassifyTemplateId(htmlMessage);
        if (templateId is null)
        {
            _logger.LogWarning(
                "Could not classify Identity mail flow from HTML markers; not sending. Recipient={Recipient} subject={Subject}",
                RedactEmail(email),
                subject);
            return;
        }

        var actionUrl = MailerIdentityEmailFlowClassifier.ExtractCallbackUrl(htmlMessage);
        if (string.IsNullOrWhiteSpace(actionUrl))
        {
            _logger.LogWarning("No callback URL found in Identity htmlMessage; not sending to {Recipient}", RedactEmail(email));
            return;
        }

        var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        var userName = PickDisplayName(user, email);

        var locale = ResolveLocale(options);

        var request = new SendTemplatedEmailRequest();
        request.To.Add(email);
        request.TemplateId = templateId;
        request.Locale = locale;
        request.Params["action_link"] = SanitizeHeaderInjection(actionUrl.Trim());
        request.Params["user_name"] = SanitizeHeaderInjection(userName);

        try
        {
            var response = await _mailerWorkerClient.SendTemplatedEmailAsync(request).ConfigureAwait(false);
            if (response is null)
            {
                _logger.LogWarning(
                    "Mailer worker client returned null (misconfigured URL?). Mail not sent to {Recipient}.",
                    RedactEmail(email));
                return;
            }

            _logger.LogInformation(
                "Templated Identity mail queued worker correlation={Correlation} template={Template} locale={Locale}",
                response.CorrelationId,
                templateId,
                locale);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send templated mail to {Recipient}", RedactEmail(email));
            throw;
        }
    }

    private static string PickDisplayName(ApplicationUser? user, string email)
    {
        if (user is null)
        {
            return LocalPart(email);
        }

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            return user.UserName!;
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return LocalPart(user.Email!);
        }

        return LocalPart(email);
    }

    private static string LocalPart(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : email;
    }

    private string ResolveLocale(MailOptions options)
    {
        var culture = CultureInfo.CurrentUICulture;
        if (!string.IsNullOrWhiteSpace(culture.Name))
        {
            return culture.Name;
        }

        return string.IsNullOrWhiteSpace(options.DefaultLocale) ? "en" : options.DefaultLocale.Trim();
    }

    /// <summary>
    /// Strips CR/LF/NUL so user-controlled fragments cannot inject SMTP/MIME header boundaries if a template misuses them.
    /// </summary>
    private static string SanitizeHeaderInjection(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\0", string.Empty, StringComparison.Ordinal);

    private static string RedactEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "(empty)";
        }

        var at = email.IndexOf('@');
        return at <= 1 ? "***" : email[0] + "***@" + email[(at + 1)..];
    }
}
