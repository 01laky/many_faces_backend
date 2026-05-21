namespace BeDemo.Api.Models.Requests.OperatorAi;

public sealed class UpdateOperatorAiPublicStatsSettingsRequest
{
    public string PublicStatsMode { get; set; } = string.Empty;

    public int LiveMaxParallelBundleCalls { get; set; }
}
