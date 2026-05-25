namespace BeDemo.Api.Tests;

/// <summary>
/// xUnit collection that runs localization rate-limit tests sequentially on one shared host.
/// </summary>
/// <remarks>
/// <see cref="LocalizationRateLimit429Tests"/> share a single <see cref="RateLimitedLocalizationWebApplicationFactory"/>
/// and the same in-memory <c>localization-read</c> counter per client IP. Tests use one ordered scenario per class
/// (see <see cref="LocalizationRateLimit429Tests"/>) because xUnit does not guarantee method execution order.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationRateLimitCollection : ICollectionFixture<RateLimitedLocalizationWebApplicationFactory>
{
	/// <summary>Collection name referenced by <see cref="CollectionAttribute"/> on test classes.</summary>
	public const string Name = "Localization rate limit (serial)";
}
