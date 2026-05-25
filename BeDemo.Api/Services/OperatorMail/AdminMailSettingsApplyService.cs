using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Services.OperatorMail;

namespace BeDemo.Api.Services.OperatorMail;

public sealed class AdminMailSettingsApplyService
{
    public OperatorMailSettingsValues Merge(
        OperatorMailSettingsValues current,
        UpdateAdminMailSettingsRequest request,
        string? updatedByUserId)
    {
        var workerToken = ResolveSecretWrite(request.WorkerAuthToken, current.WorkerAuthTokenPlaintext);
        var smtpPassword = ResolveSecretWrite(request.Smtp?.Password, current.SmtpPasswordPlaintext);

        return current with
        {
            Enabled = request.Enabled,
            DefaultLocale = request.DefaultLocale?.Trim() ?? current.DefaultLocale,
            WorkerGrpcUrl = request.WorkerGrpcUrl?.Trim(),
            WorkerAuthTokenPlaintext = workerToken,
            SmtpHost = request.Smtp?.Host?.Trim() ?? current.SmtpHost,
            SmtpPort = request.Smtp?.Port ?? current.SmtpPort,
            SmtpStartTls = request.Smtp?.StartTls ?? current.SmtpStartTls,
            SmtpUser = request.Smtp?.User?.Trim(),
            SmtpPasswordPlaintext = smtpPassword,
            FromEmail = request.From?.Email?.Trim() ?? current.FromEmail,
            FromDisplayName = request.From?.DisplayName?.Trim(),
            PortalPublicBaseUrl = request.RegistrationLinks?.PortalPublicBaseUrl?.Trim() ?? current.PortalPublicBaseUrl,
            CompleteRegistrationPathTemplate = request.RegistrationLinks?.CompleteRegistrationPathTemplate?.Trim()
                ?? current.CompleteRegistrationPathTemplate,
            MobileDeepLinkBase = request.RegistrationLinks?.MobileDeepLinkBase?.Trim() ?? current.MobileDeepLinkBase,
            PreferMobileDeepLinkWhenPlatformMobile = request.RegistrationLinks?.PreferMobileDeepLinkWhenPlatformMobile
                ?? current.PreferMobileDeepLinkWhenPlatformMobile,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserId = updatedByUserId,
        };
    }

    private static string? ResolveSecretWrite(string? incoming, string? current)
    {
        if (incoming is null)
            return current;

        if (incoming.Length == 0)
            return null;

        return incoming;
    }
}
