namespace BeDemo.Api.Models;

/// <summary>
/// Singleton row for global operator AI live stats Redis cache TTL (milliseconds).
/// </summary>
public class OperatorAiLiveStatsCacheSettings
{
	/// <summary>Always <c>1</c> — platform-wide singleton.</summary>
	public int Id { get; set; } = 1;

	public long TtlMilliseconds { get; set; }

	public DateTime UpdatedAtUtc { get; set; }

	public string? UpdatedByUserId { get; set; }
}
