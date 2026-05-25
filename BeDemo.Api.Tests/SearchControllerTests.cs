using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Integration tests for <see cref="Controllers.SearchController"/> health endpoint (optional search worker over gRPC).
/// </summary>
public sealed class SearchControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public SearchControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	public void Dispose() { }

	[Fact]
	public async Task GetHealth_ShouldReturnConfiguredFalse_WhenSearchDisabled_OnPublicFace()
	{
		var client = _factory.CreateFaceClient("public");
		var response = await client.GetAsync("/api/search/health");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var dto = await response.Content.ReadFromJsonAsync<SearchHealthDto>(
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		dto.Should().NotBeNull();
		dto!.Configured.Should().BeFalse();
		dto.Reachable.Should().BeFalse();
	}

	[Fact]
	public async Task GetHealth_ShouldReturnUnauthorized_OnAdminFace_WithoutJwt()
	{
		var client = _factory.CreateFaceClient("admin");
		var response = await client.GetAsync("/api/search/health");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}
}
