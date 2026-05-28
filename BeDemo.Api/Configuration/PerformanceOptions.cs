namespace BeDemo.Api.Configuration;

/// <summary>Runtime performance tuning (BE-RP1…BE-RP35).</summary>
public sealed class PerformanceOptions
{
	public const string SectionName = "Performance";

	/// <summary>JWT access-token version cache TTL seconds (BE-RP1).</summary>
	public int AccessTokenVersionCacheSeconds { get; set; } = 45;

	/// <summary>Faces config response cache TTL seconds (BE-RP2).</summary>
	public int FacesConfigCacheSeconds { get; set; } = 120;

	/// <summary>Capabilities cache TTL seconds (BE-RP26).</summary>
	public int CapabilitiesCacheSeconds { get; set; } = 45;

	/// <summary>Platform stats dashboard cache TTL seconds (BE-RP4).</summary>
	public int PlatformStatsCacheSeconds { get; set; } = 45;

	/// <summary>Public stats snapshot cache TTL seconds (BE-RP4).</summary>
	public int PublicStatsCacheSeconds { get; set; } = 60;

	/// <summary>Admin search autocomplete cache TTL seconds (BE-RP6).</summary>
	public int AdminSearchAutocompleteCacheSeconds { get; set; } = 15;

	/// <summary>Search outbox processor max parallel gRPC calls (BE-RP5).</summary>
	public int SearchOutboxMaxParallelGrpc { get; set; } = 4;

	/// <summary>When true, append EF TagWith comments (BE-RP29). Default off in Production.</summary>
	public bool EfQueryTagsEnabled { get; set; }

	/// <summary>Upload serve Cache-Control max-age seconds (BE-RP28).</summary>
	public int UploadServeCacheMaxAgeSeconds { get; set; } = 300;

	/// <summary>Hub user display snippet cache seconds (BE-RP14).</summary>
	public int HubUserDisplayCacheSeconds { get; set; } = 60;

	/// <summary>Default gRPC deadline seconds for search worker (BE-RP32).</summary>
	public int SearchGrpcDeadlineSeconds { get; set; } = 15;
}
