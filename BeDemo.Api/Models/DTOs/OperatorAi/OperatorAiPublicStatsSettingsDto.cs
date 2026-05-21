namespace BeDemo.Api.Models.DTOs.OperatorAi;

public sealed class OperatorAiPublicStatsSettingsDto
{
    public string PublicStatsMode { get; set; } = string.Empty;

    public int LiveMaxParallelBundleCalls { get; set; }

    public string DefaultPublicStatsMode { get; set; } = string.Empty;

    public int DefaultLiveMaxParallelBundleCalls { get; set; }

    public int MinLiveMaxParallelBundleCalls { get; set; }

    public int MaxLiveMaxParallelBundleCalls { get; set; }
}
