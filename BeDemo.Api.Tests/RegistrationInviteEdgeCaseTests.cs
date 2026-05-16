using System.Net;
using System.Net.Http.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class RegistrationInviteEdgeCaseTests : IClassFixture<RegistrationInviteWebApplicationFactory>, IDisposable
{
    private readonly RegistrationInviteWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RegistrationInviteEdgeCaseTests(RegistrationInviteWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateUnscopedClient();
    }

    [Fact]
    public async Task Request_ShouldReturnOk_WhenEmailEmpty_ButModelInvalidMayBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register/request", new { email = "" });
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Prefill_ShouldReturn400_WhenHashMissing()
    {
        var response = await _client.GetAsync("/api/oauth2/register/prefill");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Complete_ShouldReturn400_WhenHashUnknown()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register/complete", new RegisterCompleteDto
        {
            Hash = "unknown-hash-value",
            Code = "ABC123",
            Password = "Test123!@#",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Complete_ShouldReturnTokens_WhenHashAndCodeValid()
    {
        var email = $"reg_{Guid.NewGuid():N}@test.com";
        var (hash, code) = await RequestInviteAsync(email);

        var response = await _client.PostAsJsonAsync("/api/oauth2/register/complete", new RegisterCompleteDto
        {
            Hash = hash,
            Code = code,
            Password = "Test123!@#",
            FirstName = "Test",
            LastName = "User",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RegisterCompleteResponseDto>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.Email.Should().Be(email);
    }

    [Fact]
    public async Task Complete_ShouldFail_WhenCodeWrongForHash()
    {
        var email = $"reg_{Guid.NewGuid():N}@test.com";
        var (hash, _) = await RequestInviteAsync(email);

        var response = await _client.PostAsJsonAsync("/api/oauth2/register/complete", new RegisterCompleteDto
        {
            Hash = hash,
            Code = "WRONG1",
            Password = "Test123!@#",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Resend_ShouldRotateHash_SoOldHashNoLongerCompletes()
    {
        var email = $"resend_{Guid.NewGuid():N}@test.com";
        var (oldHash, oldCode) = await RequestInviteAsync(email);

        var resend = await _client.PostAsJsonAsync("/api/oauth2/register/resend", new { email, locale = "en" });
        resend.StatusCode.Should().Be(HttpStatusCode.OK);

        var completeOld = await _client.PostAsJsonAsync("/api/oauth2/register/complete", new RegisterCompleteDto
        {
            Hash = oldHash,
            Code = oldCode,
            Password = "Test123!@#",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
        });
        completeOld.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _factory.CapturingMailer.LastRequest.Should().NotBeNull();
        var newCode = _factory.CapturingMailer.LastRequest!.Params["registration_code"];
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invite = ctx.RegistrationInvites.OrderByDescending(i => i.CreatedAtUtc).First(i => i.Email == email);
        invite.LinkHash.Should().NotBe(oldHash);

        var completeNew = await _client.PostAsJsonAsync("/api/oauth2/register/complete", new RegisterCompleteDto
        {
            Hash = invite.LinkHash,
            Code = newCode,
            Password = "Test123!@#",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
        });
        completeNew.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Complete_ShouldFail_WhenCodeFromOtherInviteUsedWithHash()
    {
        var emailA = $"pairA_{Guid.NewGuid():N}@test.com";
        var emailB = $"pairB_{Guid.NewGuid():N}@test.com";
        var (hashA, _) = await RequestInviteAsync(emailA);
        var (_, codeB) = await RequestInviteAsync(emailB);

        var response = await _client.PostAsJsonAsync("/api/oauth2/register/complete", new RegisterCompleteDto
        {
            Hash = hashA,
            Code = codeB,
            Password = "Test123!@#",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LegacyRegister_ShouldReturn400_Deprecated()
    {
        var response = await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email = $"old_{Guid.NewGuid():N}@test.com",
            password = "Test123!@#",
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<(string Hash, string Code)> RequestInviteAsync(string email)
    {
        _factory.CapturingMailer.Reset();
        var response = await _client.PostAsJsonAsync("/api/oauth2/register/request", new RegisterRequestDto
        {
            Email = email,
            FirstName = "A",
            LastName = "B",
            Locale = "en",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.CapturingMailer.LastRequest.Should().NotBeNull();
        _factory.CapturingMailer.LastRequest!.TemplateId.Should().Be(MailTemplateIds.AccountRegistrationCode);
        var code = _factory.CapturingMailer.LastRequest.Params["registration_code"];

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invite = ctx.RegistrationInvites.OrderByDescending(i => i.CreatedAtUtc).First(i => i.Email == email);
        return (invite.LinkHash, code);
    }

    public void Dispose() => _client.Dispose();
}
