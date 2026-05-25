using System.Net;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Regression anchor for <see cref="StoriesController"/> authorization policy.
/// The suite intentionally starts with a **401-without-auth** check so refactors to `[AllowAnonymous]`
/// do not silently widen read access to story content. Expand with happy-path CRUD once fixtures exist.
/// </summary>
public class StoriesControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public StoriesControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task GetStories_WithoutAuth_Returns401()
	{
		var client = _factory.CreateClient();
		var response = await client.GetAsync("/api/stories");
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}
}
