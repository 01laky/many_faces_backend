using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeDemo.Api.Models.DTOs.OperatorAi;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiConversationsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public OperatorAiConversationsControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    public void Dispose() { }

    [Fact]
    public async Task List_returns_Forbidden_on_public_face_scope()
    {
        var client = _factory.CreateClient();
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/operator-ai/conversations");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Crud_roundtrip_on_admin_face_scope()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await client.PostAsJsonAsync("/api/operator-ai/conversations", new { title = "Support thread" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<OperatorAiConversationListItemDto>();
        created!.Title.Should().Be("Support thread");

        var list = await client.GetAsync("/api/operator-ai/conversations");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await list.Content.ReadFromJsonAsync<List<OperatorAiConversationListItemDto>>();
        items!.Should().ContainSingle(c => c.Id == created.Id);

        var messages = await client.GetAsync($"/api/operator-ai/conversations/{created.Id}/messages");
        messages.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/api/operator-ai/conversations/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
