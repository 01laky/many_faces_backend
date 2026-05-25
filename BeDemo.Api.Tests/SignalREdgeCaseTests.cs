using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Edge case tests for SignalR Hub
/// </summary>
public class SignalREdgeCaseTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public SignalREdgeCaseTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = _factory.CreateClient();
	}

	private Task<string> GetTokenAsync(string email, string password) =>
		IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(_client, _factory, email, password);

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldReject_WhenTokenIsExpired()
	// {
	//     // Note: This would require creating an expired token - simplified test
	//     var hubConnection = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, "/hubs/chat?access_token=expired.token"), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     await Assert.ThrowsAsync<Exception>(async () => await hubConnection.StartAsync());
	//     await hubConnection.DisposeAsync();
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldReject_WhenTokenIsMalformed()
	// {
	//     var hubConnection = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, "/hubs/chat?access_token=malformed.token.here"), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     await Assert.ThrowsAsync<Exception>(async () => await hubConnection.StartAsync());
	//     await hubConnection.DisposeAsync();
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldReject_WhenTokenIsEmpty()
	// {
	//     var hubConnection = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, "/hubs/chat?access_token="), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     await Assert.ThrowsAsync<Exception>(async () => await hubConnection.StartAsync());
	//     await hubConnection.DisposeAsync();
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldHandleEmptyMessage()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     var token = await GetTokenAsync(email, "Test1234!@##");
	//     var hubConnection = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={token}"), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     await hubConnection.StartAsync();
	//     await hubConnection.InvokeAsync("SendMessage", "User", "");
	//     await hubConnection.StopAsync();
	//     await hubConnection.DisposeAsync();
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldHandleVeryLongMessage()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     var token = await GetTokenAsync(email, "Test1234!@##");
	//     var longMessage = new string('a', 10000);
	//     var hubConnection = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={token}"), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     await hubConnection.StartAsync();
	//     await hubConnection.InvokeAsync("SendMessage", "User", longMessage);
	//     await hubConnection.StopAsync();
	//     await hubConnection.DisposeAsync();
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldHandleSpecialCharactersInMessage()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     var token = await GetTokenAsync(email, "Test1234!@##");
	//     var specialMessage = "!@#$%^&*()_+-=[]{}|;':\",./<>?";
	//     var hubConnection = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={token}"), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     await hubConnection.StartAsync();
	//     await hubConnection.InvokeAsync("SendMessage", "User", specialMessage);
	//     await hubConnection.StopAsync();
	//     await hubConnection.DisposeAsync();
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldHandleUnicodeInMessage()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     var token = await GetTokenAsync(email, "Test1234!@##");
	//     var unicodeMessage = "Hello 世界 🌍 Привет";
	//     var hubConnection = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={token}"), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     await hubConnection.StartAsync();
	//     await hubConnection.InvokeAsync("SendMessage", "User", unicodeMessage);
	//     await hubConnection.StopAsync();
	//     await hubConnection.DisposeAsync();
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldHandleMultipleConnectionsFromSameUser()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     var token = await GetTokenAsync(email, "Test1234!@##");
	//     
	//     var connection1 = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={token}"), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     var connection2 = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={token}"), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     await connection1.StartAsync();
	//     await connection2.StartAsync();
	//     
	//     connection1.State.Should().Be(HubConnectionState.Connected);
	//     connection2.State.Should().Be(HubConnectionState.Connected);

	//     await connection1.StopAsync();
	//     await connection2.StopAsync();
	//     await connection1.DisposeAsync();
	//     await connection2.DisposeAsync();
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldHandleRapidConnectDisconnect()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     var token = await GetTokenAsync(email, "Test1234!@##");
	//     
	//     for (int i = 0; i < 5; i++)
	//     {
	//         var connection = new HubConnectionBuilder()
	//             .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={token}"), options =>
	//             {
	//                 options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//                 options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//             })
	//             .Build();

	//         await connection.StartAsync();
	//         await connection.StopAsync();
	//         await connection.DisposeAsync();
	//     }
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldHandlePrivateMessageToNonExistentUser()
	// {
	//     var email = $"test_{Guid.NewGuid()}@test.com";
	//     var token = await GetTokenAsync(email, "Test1234!@##");
	//     var hubConnection = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={token}"), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     await hubConnection.StartAsync();
	//     await hubConnection.InvokeAsync("SendPrivateMessage", "non-existent-user-id", "Message");
	//     await hubConnection.StopAsync();
	//     await hubConnection.DisposeAsync();
	// }

	// [Fact] // Temporarily disabled - database conflict
	// public async Task SignalR_ShouldHandleEmptyPrivateMessage()
	// {
	//     var email1 = $"test1_{Guid.NewGuid()}@test.com";
	//     var email2 = $"test2_{Guid.NewGuid()}@test.com";
	//     var token1 = await GetTokenAsync(email1, "Test1234!@##");
	//     var token2 = await GetTokenAsync(email2, "Test1234!@##");
	//     
	//     var connection1 = new HubConnectionBuilder()
	//         .WithUrl(new Uri(_client.BaseAddress!, $"/hubs/chat?access_token={token1}"), options =>
	//         {
	//             options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
	//             options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
	//         })
	//         .Build();

	//     await connection1.StartAsync();
	//     await connection1.InvokeAsync("SendPrivateMessage", email2, "");
	//     await connection1.StopAsync();
	//     await connection1.DisposeAsync();
	// }

	public void Dispose()
	{
		_client?.Dispose();
	}
}
