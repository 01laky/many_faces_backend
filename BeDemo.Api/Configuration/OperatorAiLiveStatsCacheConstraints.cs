namespace BeDemo.Api.Configuration;

/// <summary>Validation bounds for live stats Redis bundle cache TTL (Admin PUT + options fallback).</summary>
public static class OperatorAiLiveStatsCacheConstraints
{
	public const long DefaultTtlMilliseconds = 300_000;
	public const long MinTtlMilliseconds = 30_000;
	public const long MaxTtlMilliseconds = 3_600_000;
}
