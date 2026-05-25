namespace BeDemo.Api.Models.DTOs.Admin;

public sealed class AdminPushSettingsDto
{
    public bool Enabled { get; init; }

    public string? WorkerGrpcUrl { get; init; }

    public bool HasWorkerAuthToken { get; init; }

    public required AdminPushFirebaseSettingsDto Firebase { get; init; }

    public required AdminPushDefaultsSettingsDto Defaults { get; init; }

    public int GrpcDeadlineSeconds { get; init; }

    public required AdminPushTransportSettingsDto Transport { get; init; }

    public required string EffectiveStatus { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public string? UpdatedByUserId { get; init; }
}

public sealed class AdminPushFirebaseSettingsDto
{
    public string? ProjectId { get; init; }

    public bool HasCredentials { get; init; }
}

public sealed class AdminPushDefaultsSettingsDto
{
    public required string TitleLocKey { get; init; }

    public required string BodyLocKey { get; init; }

    public string? AndroidChannelId { get; init; }
}

public sealed class AdminPushTransportSettingsDto
{
    public bool TlsConfiguredViaEnv { get; init; }

    public bool MtlsConfiguredViaEnv { get; init; }
}

public sealed class AdminPushTestFcmResultDto
{
    public bool FcmReachable { get; init; }

    public string? ProjectId { get; init; }

    public string? Message { get; init; }
}
