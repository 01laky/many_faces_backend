namespace BeDemo.Api.Configuration;

/// <summary>LiveKit / SFU settings for VideoLounge tokens. Stub mode works without a real SFU for tests.</summary>
public sealed class VideoLoungeOptions
{
	public const string SectionName = "VideoLounge";

	public string? LiveKitUrl { get; set; }

	public string? ApiKey { get; set; }

	public string? ApiSecret { get; set; }

	/// <summary>When true (default in Development), emit stub tokens the portal can mock-connect with.</summary>
	public bool UseStubTokens { get; set; } = true;

	public int TokenTtlMinutes { get; set; } = 10;
}
