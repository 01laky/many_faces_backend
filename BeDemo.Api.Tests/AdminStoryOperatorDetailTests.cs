using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Operator GET /api/stories/{id} must match list inventory (draft/expired), not portal live-only rules.
/// </summary>
public sealed class AdminStoryOperatorDetailTests
	: IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly HttpClient _adminClient;
	private string? _token;

	public AdminStoryOperatorDetailTests(CustomWebApplicationFactory<Program> factory)
	{
		_adminClient = factory.CreateFaceClient("admin");
	}

	private async Task AuthorizeAdminAsync()
	{
		_token ??= await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(_adminClient);
		_adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
	}

	[Fact]
	public async Task GetStoryDetail_operator_can_load_draft_story_for_face()
	{
		await AuthorizeAdminAsync();

		var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(
			_adminClient,
			_token!,
			"public");

		var create = await _adminClient.PostAsJsonAsync(
			"/api/stories",
			new { title = $"Operator detail {Guid.NewGuid():N}", faceIds = new[] { faceId } });
		create.StatusCode.Should().Be(HttpStatusCode.Created);
		var created = await create.Content.ReadFromJsonAsync<JsonElement>();
		var storyId = created.GetProperty("id").GetInt32();

		var items = await IntegrationTestPaginatedList.GetListItemsAsync(
			_adminClient,
			$"/api/stories?faceId={faceId}&page=1&pageSize=50");
		items.Should().Contain(i => i.GetProperty("id").GetInt32() == storyId);

		var detail = await _adminClient.GetAsync($"/api/stories/{storyId}?faceId={faceId}");
		detail.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	public void Dispose() => _adminClient.Dispose();
}
