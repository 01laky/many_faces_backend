using System.Net;
using System.Net.Http.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BeDemo.Api.Tests;

/// <summary>Test helper: full request → read code from fake mailer → complete (used by integration tests).</summary>
internal static class IntegrationTestRegistration
{
    public static async Task<RegisterCompleteResponseDto> CompleteRegistrationAsync(
        HttpClient client,
        RegistrationInviteWebApplicationFactory factory,
        string email,
        string password,
        string firstName = "Test",
        string lastName = "User")
    {
        factory.CapturingMailer.Reset();
        var requestResponse = await client.PostAsJsonAsync("/api/oauth2/register/request", new RegisterRequestDto
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Locale = "en",
        });
        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.CapturingMailer.LastRequest.Should().NotBeNull();
        var code = factory.CapturingMailer.LastRequest!.Params["registration_code"];

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invite = ctx.RegistrationInvites.OrderByDescending(i => i.CreatedAtUtc).First(i => i.Email == email);

        var completeResponse = await client.PostAsJsonAsync("/api/oauth2/register/complete", new RegisterCompleteDto
        {
            Hash = invite.LinkHash,
            Code = code,
            Password = password,
            FirstName = firstName,
            LastName = lastName,
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
        });
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await completeResponse.Content.ReadFromJsonAsync<RegisterCompleteResponseDto>();
        body.Should().NotBeNull();
        return body!;
    }
}
