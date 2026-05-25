using ManyFaces.Push.V1;

namespace BeDemo.Api.Services.OperatorPush;

internal static class OperatorPushProtoMapper
{
    internal static FcmCredentialsConfig? ToProto(OperatorPushSettingsValues values)
    {
        if (!values.HasFirebaseCredentials)
            return null;

        return new FcmCredentialsConfig
        {
            ServiceAccountJson = values.FirebaseServiceAccountJsonPlaintext!,
        };
    }

    internal static SendPushRequest EnrichRequest(SendPushRequest request, OperatorPushSettingsValues values)
    {
        if (!values.IsSendAllowed || request.Fcm is not null)
            return request;

        var fcm = ToProto(values);
        if (fcm is null)
            return request;

        request.Fcm = fcm;
        return request;
    }
}
