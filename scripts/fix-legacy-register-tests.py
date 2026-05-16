#!/usr/bin/env python3
"""Point integration tests at IntegrationTestRegistration instead of legacy POST /api/oauth2/register."""
from __future__ import annotations

import re
from pathlib import Path

TESTS = Path(__file__).resolve().parents[1] / "BeDemo.Api.Tests"

SKIP_SNIPPETS = (
    "LegacyRegister",
    "LegacyEndpoint",
    "ShouldReturnDeprecated",
    "ShouldReturn400_Deprecated",
    "OAuth2_Register_ShouldRespond",
    "Legacy register",
    "deprecated (400)",
)

GET_TOKEN_OLD = """    private async Task<string> GetTokenAsync(string email, string password)
    {
        await _client.PostAsJsonAsync("/api/oauth2/register", new { email, password, firstName = "Test", lastName = "User" });
        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = password
        };
        var tokenResponse = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        return tokenData!.AccessToken;
    }"""

GET_TOKEN_NEW = """    private Task<string> GetTokenAsync(string email, string password) =>
        IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(_client, _factory, email, password);"""

GET_TENANT_OLD_START = """        await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Face",
            lastName = "Tenant",
        });

        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = password,
        };

        HttpResponseMessage? response = null;
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(150 * (i + 1));
            response = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
            if (response.StatusCode == HttpStatusCode.OK)
                break;
        }

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        tokenResponse.Should().NotBeNull();
        _tenantToken = tokenResponse!.AccessToken;"""

GET_TENANT_NEW = """        _tenantToken = await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(
            _client,
            _factory,
            email,
            password,
            "Face",
            "Tenant");"""

REGISTER_LOGIN_OLD = """        await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Moderation",
            lastName = "Tester"
        });

        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = password
        };

        HttpResponseMessage? response = null;
        for (int i = 0; i < 15; i++)
        {
            await Task.Delay(150 * (i + 1));
            response = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
            if (response.StatusCode == HttpStatusCode.OK)
                break;
        }

        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        return tokenResponse!.AccessToken;"""

REGISTER_LOGIN_NEW = """        return await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(
            _client,
            _factory,
            email,
            password,
            "Moderation",
            "Tester");"""

# register + token retry block (SignalRHubTests style)
SIGNALR_REG_BLOCK = re.compile(
    r"""
        var\ reg\ =\ await\ _client\.PostAsJsonAsync\(\s*
            "/api/oauth2/register",\s*
            new\ \{\ email,\ password,\ firstName\ =\ "[^"]+",\ lastName\ =\ "[^"]+"\ \}\);\s*
        reg\.StatusCode\.Should\(\)\.Be\(HttpStatusCode\.OK\);\s*
        HttpResponseMessage\?\ tokResp\ =\ null;\s*
        for\ \(var\ i\ =\ 0;\ i\ <\ 15;\ i\+\+\)\s*
        \{\s*
            await\ Task\.Delay\(150\ \*\ \(i\ \+\ 1\)\);\s*
            tokResp\ =\ await\ _client\.PostAsJsonAsync\(\s*
                "/api/oauth2/token",\s*
                new\ OAuth2TokenRequest\s*
                \{\s*
                    GrantType\ =\ "password",\s*
                    ClientId\ =\ "be-demo-client",\s*
                    ClientSecret\ =\ "be-demo-secret-very-strong-key",\s*
                    Username\ =\ email,\s*
                    Password\ =\ password,\s*
                \}\);\s*
            if\ \(tokResp\.StatusCode\ ==\ HttpStatusCode\.OK\)\s*
                break;\s*
        \}\s*
        tokResp\.Should\(\)\.NotBeNull\(\);\s*
        tokResp!\.StatusCode\.Should\(\)\.Be\(HttpStatusCode\.OK\);\s*
        var\ tokenData\ =\ await\ tokResp\.Content\.ReadFromJsonAsync<OAuth2TokenResponse>\(\);\s*
        tokenData!\.AccessToken\.Should\(\)\.NotBeNullOrEmpty\(\);
    """,
    re.VERBOSE,
)

SIGNALR_REPL = """        var tokenData = await IntegrationTestRegistration.CompleteRegistrationAsync(
            _client,
            _factory,
            email,
            password,
            "S",
            "R");
        tokenData.AccessToken.Should().NotBeNullOrEmpty();"""


def patch_file(path: Path) -> bool:
    text = path.read_text()
    orig = text

    if GET_TOKEN_OLD in text:
        text = text.replace(GET_TOKEN_OLD, GET_TOKEN_NEW)
    if GET_TENANT_OLD_START in text:
        text = text.replace(GET_TENANT_OLD_START, GET_TENANT_NEW)
    if REGISTER_LOGIN_OLD in text:
        text = text.replace(REGISTER_LOGIN_OLD, REGISTER_LOGIN_NEW)

    text = SIGNALR_REG_BLOCK.sub(SIGNALR_REPL, text)

    # Simple single-line register expecting OK -> CompleteRegistrationAsync
    text = re.sub(
        r'await _client\.PostAsJsonAsync\("/api/oauth2/register", new \{ email = \$"([^"]+)", password = "Test123!@#"\ }\);',
        r'await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, $"test_{Guid.NewGuid()}@test.com", "Test123!@#");',
        text,
    )

    # registerResponse multiline common in LoginTests/UserProfileTests
    text = re.sub(
        r'var registerResponse = await _client\.PostAsJsonAsync\("/api/oauth2/register", new\s*\{\s*email,\s*password,\s*firstName = "([^"]+)",\s*lastName = "([^"]+)"\s*\}\);\s*registerResponse\.StatusCode\.Should\(\)\.Be\(HttpStatusCode\.OK\);',
        r'await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, password, "\1", "\2");',
        text,
        flags=re.MULTILINE,
    )

    # Performance: concurrent register lines
    text = text.replace(
        '_client.PostAsJsonAsync("/api/oauth2/register", new { email = $"test_{Guid.NewGuid()}@test.com", password = "Test123!@#" })',
        'IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, $"test_{Guid.NewGuid()}@test.com", "Test123!@#")',
    )

    text = text.replace(
        'var response = await _client.PostAsJsonAsync("/api/oauth2/register", new { email = $"test_{Guid.NewGuid()}@test.com", password = "Test123!@#" });',
        'var response = (HttpResponseMessage?)null; await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, $"test_{Guid.NewGuid()}@test.com", "Test123!@#");',
    )

    if text != orig:
        path.write_text(text)
        return True
    return False


def main() -> None:
    changed = []
    for path in sorted(TESTS.glob("*.cs")):
        if path.name in (
            "IntegrationTestRegistration.cs",
            "AclTestClients.cs",
            "ApiIntegrationTests.cs",
            "OAuth2ControllerTests.cs",
            "OAuth2EdgeCaseTests.cs",
            "RegistrationInviteEdgeCaseTests.cs",
        ):
            continue
        if '"/api/oauth2/register"' not in path.read_text():
            continue
        if patch_file(path):
            changed.append(path.name)
    print("patched:", ", ".join(changed) if changed else "(none)")


if __name__ == "__main__":
    main()
