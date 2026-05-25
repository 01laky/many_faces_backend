using BeDemo.Api.Models.Requests.Admin;

namespace BeDemo.Api.Services.OperatorPush;

public sealed class AdminPushSettingsApplyService
{
    public OperatorPushSettingsValues Merge(
        OperatorPushSettingsValues current,
        UpdateAdminPushSettingsRequest request,
        string? updatedByUserId)
    {
        var workerToken = ResolveSecretWrite(request.WorkerAuthToken, current.WorkerAuthTokenPlaintext);
        var firebaseJson = ResolveSecretWrite(request.Firebase?.ServiceAccountJson, current.FirebaseServiceAccountJsonPlaintext);
        string? projectId = current.FirebaseProjectId;
        if (firebaseJson is not null)
        {
            if (FirebaseServiceAccountValidator.TryValidate(firebaseJson, out var parsedProjectId, out _))
                projectId = parsedProjectId;
        }
        else if (request.Firebase?.ServiceAccountJson is { Length: 0 })
        {
            projectId = null;
        }

        return current with
        {
            Enabled = request.Enabled,
            WorkerGrpcUrl = request.WorkerGrpcUrl?.Trim(),
            WorkerAuthTokenPlaintext = workerToken,
            FirebaseProjectId = projectId,
            FirebaseServiceAccountJsonPlaintext = firebaseJson,
            DefaultTitleLocKey = request.Defaults?.TitleLocKey?.Trim() ?? current.DefaultTitleLocKey,
            DefaultBodyLocKey = request.Defaults?.BodyLocKey?.Trim() ?? current.DefaultBodyLocKey,
            DefaultAndroidChannelId = request.Defaults?.AndroidChannelId?.Trim(),
            GrpcDeadlineSeconds = request.GrpcDeadlineSeconds ?? current.GrpcDeadlineSeconds,
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
