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
    /// <summary>True when operator mail is fully configured for sends.</summary>
    public bool Configured { get; init; }

    public string? EffectiveStatus { get; init; }
}

public sealed class AdminInfraPushWorkerConfigDto
{
    /// <summary>True when operator push is fully configured for sends.</summary>
    public bool Configured { get; init; }

    public string? EffectiveStatus { get; init; }

    /// <summary>Count of <c>UserPushDevices</c> rows for the calling operator account.</summary>
    public int RegisteredDeviceCount { get; init; }
}
