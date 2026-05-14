using System.Text.RegularExpressions;

namespace BeDemo.Api.Services;

/// <summary>
/// Pure helpers for mapping Identity's pre-rendered HTML fragment into worker <c>template_id</c> + callback URL.
/// Kept stateless so unit tests do not need EF or <see cref="Microsoft.AspNetCore.Identity.UserManager{TUser}"/>.
/// </summary>
public static class MailerIdentityEmailFlowClassifier
{
    private static readonly Regex FirstHttpUrl = new(
        @"https?://[^\s""'<>]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>Same identifiers as many_faces_mailer <c>TemplateCatalog</c>.</summary>
    public const string TemplateIdentityEmailConfirm = "identity_email_confirm";

    public const string TemplateIdentityPasswordReset = "identity_password_reset";

    public static string? ClassifyTemplateId(string? htmlMessage)
    {
        if (string.IsNullOrEmpty(htmlMessage))
        {
            return null;
        }

        if (htmlMessage.Contains("ResetPassword", StringComparison.OrdinalIgnoreCase)
            || htmlMessage.Contains("/Account/ResetPassword", StringComparison.OrdinalIgnoreCase))
        {
            return TemplateIdentityPasswordReset;
        }

        if (htmlMessage.Contains("ConfirmEmail", StringComparison.OrdinalIgnoreCase)
            || htmlMessage.Contains("/Account/ConfirmEmail", StringComparison.OrdinalIgnoreCase))
        {
            return TemplateIdentityEmailConfirm;
        }

        return null;
    }

    public static string? ExtractCallbackUrl(string? htmlMessage)
    {
        if (string.IsNullOrEmpty(htmlMessage))
        {
            return null;
        }

        var m = FirstHttpUrl.Match(htmlMessage);
        return m.Success ? m.Value : null;
    }
}
