using ManyFaces.Mailer.V1;

namespace BeDemo.Api.Services.OperatorMail;

internal static class OperatorMailProtoMapper
{
    internal static SmtpTransportConfig ToProto(OperatorMailSettingsValues values) =>
        new()
        {
            Host = values.SmtpHost,
            Port = values.SmtpPort,
            StartTls = values.SmtpStartTls,
            User = values.SmtpUser ?? string.Empty,
            Password = values.SmtpPasswordPlaintext ?? string.Empty,
            FromEmail = values.FromEmail,
            FromDisplayName = values.FromDisplayName ?? string.Empty,
        };

    internal static SendTemplatedEmailRequest EnrichRequest(SendTemplatedEmailRequest request, OperatorMailSettingsValues values)
    {
        if (!values.IsSendAllowed || request.Smtp is not null)
            return request;

        request.Smtp = ToProto(values);
        return request;
    }
}
