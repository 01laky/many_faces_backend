using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// SignalR hub security matrix: anonymous negotiate attempts must fail for face-scoped hubs, while authenticated
/// scenarios (JWT in query string + long polling) prove the happy path for browser-compatible transports.
/// Complements manual checks documented in <c>docs/guides/signalr-hub-security-matrix.md</c>.
/// </summary>
public class SignalRHubTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SignalRHubTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task SignalRHub_ShouldRejectConnection_WhenNoToken()
    {
        await AssertHubRejectsWithoutTokenAsync("/public/hubs/chat");
    }

    [Fact]
    public async Task MessengerHub_ShouldRejectConnection_WhenNoToken() =>
        await AssertHubRejectsWithoutTokenAsync("/public/hubs/messenger");

    [Fact]
    public async Task ChatRoomHub_ShouldRejectConnection_WhenNoToken() =>
        await AssertHubRejectsWithoutTokenAsync("/public/hubs/chatroom");

    /// <summary>
    /// Positive-path check for browser SignalR clients that pass JWT as <c>access_token</c> on the query string
    /// (WebSockets can attach headers; long polling historically relied on query for bearer material in some stacks).
    /// Uses in-memory test server handler + long polling only to keep the test deterministic without real WebSocket infra.
    /// </summary>
    [Fact]
    public async Task ChatHub_ShouldConnect_WhenValidJwtInQueryString()
    {
        var email = $"sr_{Guid.NewGuid():N}@test.com";
        const string password = "Test123!@#";

        var tokenData = await IntegrationTestRegistration.CompleteRegistrationAsync(
            _client,
            _factory,
            email,
            password,
            "S",
            "R");
        tokenData.AccessToken.Should().NotBeNullOrEmpty();

        var hubUrl = new Uri(
            _client.BaseAddress!,
            $"/public/hubs/chat?access_token={Uri.EscapeDataString(tokenData.AccessToken)}");
        await using var hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();

        await hubConnection.StartAsync();
        hubConnection.State.Should().Be(HubConnectionState.Connected);
        await hubConnection.StopAsync();
    }

    /// <summary>
    /// All face-prefixed hubs require Bearer or <c>access_token</c> query; unauthenticated negotiate must fail.
    /// </summary>
    private async Task AssertHubRejectsWithoutTokenAsync(string relativePath)
    {
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, relativePath), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await hubConnection.StartAsync();
        });

        exception.Should().NotBeNull();
        await hubConnection.DisposeAsync();
    }

    // [Fact] // Temporarily disabled - database conflict
    // public async Task SignalRHub_ShouldConnect_WhenValidToken()
    // {
    //     // Arrange
    //     var email = $"test_{Guid.NewGuid()}@test.com";
    //     var password = "Test123!@#";

    //     // Register and get token
    //     await _client.PostAsJsonAsync("/api/oauth2/register", new
    //     {
    //         email,
    //         password,
    //         firstName = "Test",
    //         lastName = "User"
    //     });

    //     var tokenRequest = new OAuth2TokenRequest
    //     {
    //         GrantType = "password",
    //         ClientId = "be-demo-client",
    //         ClientSecret = "be-demo-secret-very-strong-key",
    //         Username = email,
    //         Password = password
    //     };

    //     var tokenResponse = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
    //     tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    //     var responseContent = await tokenResponse.Content.ReadAsStringAsync();
    //     var tokenData = await tokenResponse.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
    //     tokenData.Should().NotBeNull();
    //     tokenData!.AccessToken.Should().NotBeNullOrEmpty();

    //     // Create hub connection with token
    //     var hubConnection = new HubConnectionBuilder()
    //         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={tokenData.AccessToken}"), options =>
    //         {
    //             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
    //             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
    //         })
    //         .Build();

    //     // Act
    //     await hubConnection.StartAsync();

    //     // Assert
    //     hubConnection.State.Should().BeOneOf(HubConnectionState.Connected, HubConnectionState.Connecting);
    //     
    //     // Wait a bit for connection to fully establish
    //     await Task.Delay(100);
    //     hubConnection.State.Should().Be(HubConnectionState.Connected);

    //     // Cleanup
    //     await hubConnection.StopAsync();
    //     await hubConnection.DisposeAsync();
    // }

    // [Fact] // Temporarily disabled - database conflict
    // public async Task SignalRHub_ShouldSendAndReceiveMessage_WhenAuthenticated()
    // {
    //     // Arrange
    //     var email = $"test_{Guid.NewGuid()}@test.com";
    //     var password = "Test123!@#";

    //     // Register and get token
    //     await _client.PostAsJsonAsync("/api/oauth2/register", new
    //     {
    //         email,
    //         password,
    //         firstName = "Test",
    //         lastName = "User"
    //     });

    //     var tokenRequest = new OAuth2TokenRequest
    //     {
    //         GrantType = "password",
    //         ClientId = "be-demo-client",
    //         ClientSecret = "be-demo-secret-very-strong-key",
    //         Username = email,
    //         Password = password
    //     };

    //     var tokenResponse = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
    //     var tokenData = await tokenResponse.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
    //     tokenData.Should().NotBeNull();
    //     tokenData!.AccessToken.Should().NotBeNullOrEmpty();

    //     var messageReceived = false;
    //     string? receivedUser = null;
    //     string? receivedMessage = null;

    //     var hubConnection = new HubConnectionBuilder()
    //         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={tokenData.AccessToken}"), options =>
    //         {
    //             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
    //             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
    //         })
    //         .Build();

    //     hubConnection.On<string, string>("ReceiveMessage", (user, message) =>
    //     {
    //         messageReceived = true;
    //         receivedUser = user;
    //         receivedMessage = message;
    //     });

    //     await hubConnection.StartAsync();

    //     // Act
    //     await hubConnection.InvokeAsync("SendMessage", "TestUser", "Hello SignalR!");

    //     // Wait for message to be received
    //     await Task.Delay(500);

    //     // Assert
    //     messageReceived.Should().BeTrue();
    //     receivedUser.Should().Be("TestUser");
    //     receivedMessage.Should().Be("Hello SignalR!");

    //     // Cleanup
    //     await hubConnection.StopAsync();
    //     await hubConnection.DisposeAsync();
    // }

    // [Fact] // Temporarily disabled - database conflict
    // public async Task SignalRHub_ShouldSendPrivateMessage_WhenAuthenticated()
    // {
    //     // Arrange - Create two users
    //     var email1 = $"test1_{Guid.NewGuid()}@test.com";
    //     var email2 = $"test2_{Guid.NewGuid()}@test.com";
    //     var password = "Test123!@#";

    //     // Register users
    //     await _client.PostAsJsonAsync("/api/oauth2/register", new
    //     {
    //         email = email1,
    //         password,
    //         firstName = "Test1",
    //         lastName = "User1"
    //     });

    //     await _client.PostAsJsonAsync("/api/oauth2/register", new
    //     {
    //         email = email2,
    //         password,
    //         firstName = "Test2",
    //         lastName = "User2"
    //     });

    //     // Get tokens
    //     var tokenRequest1 = new OAuth2TokenRequest
    //     {
    //         GrantType = "password",
    //         ClientId = "be-demo-client",
    //         ClientSecret = "be-demo-secret-very-strong-key",
    //         Username = email1,
    //         Password = password
    //     };

    //     var tokenRequest2 = new OAuth2TokenRequest
    //     {
    //         GrantType = "password",
    //         ClientId = "be-demo-client",
    //         ClientSecret = "be-demo-secret-very-strong-key",
    //         Username = email2,
    //         Password = password
    //     };

    //     var tokenResponse1 = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest1);
    //     var tokenResponse2 = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest2);

    //     var tokenData1 = await tokenResponse1.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
    //     var tokenData2 = await tokenResponse2.Content.ReadFromJsonAsync<OAuth2TokenResponse>();

    //     tokenData1.Should().NotBeNull();
    //     tokenData2.Should().NotBeNull();

    //     // Create hub connections
    //     var hubConnection1 = new HubConnectionBuilder()
    //         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={tokenData1!.AccessToken}"), options =>
    //         {
    //             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
    //             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
    //         })
    //         .Build();

    //     var hubConnection2 = new HubConnectionBuilder()
    //         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={tokenData2!.AccessToken}"), options =>
    //         {
    //             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
    //             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
    //         })
    //         .Build();

    //     var messageReceived = false;
    //     string? receivedSender = null;
    //     string? receivedMessage = null;

    //     hubConnection2.On<string, string>("ReceivePrivateMessage", (sender, message) =>
    //     {
    //         messageReceived = true;
    //         receivedSender = sender;
    //         receivedMessage = message;
    //     });

    //     await hubConnection1.StartAsync();
    //     await hubConnection2.StartAsync();

    //     // Get user ID from token (simplified - in real scenario, decode JWT to get user ID)
    //     // For this test, we'll use email as user identifier
    //     var targetUserId = email2;

    //     // Act
    //     await hubConnection1.InvokeAsync("SendPrivateMessage", targetUserId, "Private message!");

    //     // Wait for message to be received
    //     await Task.Delay(500);

    //     // Assert
    //     messageReceived.Should().BeTrue();
    //     receivedMessage.Should().Be("Private message!");

    //     // Cleanup
    //     await hubConnection1.StopAsync();
    //     await hubConnection2.StopAsync();
    //     await hubConnection1.DisposeAsync();
    //     await hubConnection2.DisposeAsync();
    // }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
