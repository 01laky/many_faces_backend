namespace BeDemo.Api.Models.DTOs;

/// <summary>
/// Read-only worker configuration for admin Settings infrastructure panel (no secrets or gRPC URLs).
/// </summary>
public sealed class AdminInfraWorkerConfigDto
{
    public required AdminInfraMailWorkerConfigDto Mail { get; init; }

    public required AdminInfraPushWorkerConfigDto Push { get; init; }
}

public sealed class AdminInfraMailWorkerConfigDto
{
    /// <summary>True when <see cref="Services.MailOptions.IsEnabled"/> — mail worker gRPC would be used for smoke tests.</summary>
    public bool Configured { get; init; }
}

public sealed class AdminInfraPushWorkerConfigDto
{
    /// <summary>True when <see cref="Services.PushOptions.IsEnabled"/> — push worker gRPC would be used for smoke tests.</summary>
    public bool Configured { get; init; }

    /// <summary>Count of <c>UserPushDevices</c> rows for the calling operator account.</summary>
    public int RegisteredDeviceCount { get; init; }
}
