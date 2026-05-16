#!/usr/bin/env python3
"""Second pass: replace remaining legacy register patterns in BeDemo.Api.Tests."""
from __future__ import annotations

import re
from pathlib import Path

TESTS = Path(__file__).resolve().parents[1] / "BeDemo.Api.Tests"

SKIP_FILES = {
    "ApiIntegrationTests.cs",
    "OAuth2ControllerTests.cs",
    "OAuth2EdgeCaseTests.cs",
    "RegistrationInviteEdgeCaseTests.cs",
    "IntegrationTestRegistration.cs",
}

GET_AUTH_BLOCK = re.compile(
    r"""
        await\ _client\.PostAsJsonAsync\("/api/oauth2/register",\ new\s*\{\s*
            email,\s*password,\s*firstName\ =\ "[^"]+",\s*lastName\ =\ "[^"]+"\s*\}\);\s*
        var\ tokenRequest\ =\ new\ OAuth2TokenRequest\s*\{[^}]+\};\s*
        HttpResponseMessage\?\ response\ =\ null;\s*
        for\ \(int\ i\ =\ 0;\ i\ <\ 15;\ i\+\+\)\s*\{\s*
            await\ Task\.Delay\(150\ \*\ \(i\ \+\ 1\)\);\s*
            response\ =\ await\ _client\.PostAsJsonAsync\("/api/oauth2/token",\ tokenRequest\);\s*
            if\ \(response\.StatusCode\ ==\ HttpStatusCode\.OK\)\s*break;\s*
        \}\s*
        response!\.StatusCode\.Should\(\)\.Be\(HttpStatusCode\.OK\);\s*
        var\ tokenResponse\ =\ await\ response\.Content\.ReadFromJsonAsync<OAuth2TokenResponse>\(\);\s*
        (?:_authToken\ =\ )?tokenResponse!\.AccessToken(?:!)?;\s*
        (?:return\ _authToken;)?
    """,
    re.VERBOSE | re.MULTILINE,
)


def patch_get_auth_token(content: str, first: str, last: str) -> str:
    replacement = f"""var email = $"{{prefix}}_{{Guid.NewGuid()}}@test.com";
        var password = "Test123!@#";
        _authToken = await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(
            _client,
            _factory,
            email,
            password,
            "{first}",
            "{last}");
        return _authToken;"""
    new_content, n = GET_AUTH_BLOCK.subn(replacement, content, count=1)
    return new_content, n > 0


def patch_file(path: Path) -> bool:
    text = path.read_text()
    orig = text
    if "GetAuthTokenAsync" in text and 'await _client.PostAsJsonAsync("/api/oauth2/register"' in text:
        for first, last in [("Blog", "Tester"), ("Reel", "Tester"), ("Page", "Tester")]:
            text, _ = patch_get_auth_token(text, first, last)

    # AccessTokenVersionTests pattern
    text = re.sub(
        r'var reg = await client\.PostAsJsonAsync\("/api/oauth2/register", new \{ email, password = "Test123!@#"\ }\);\s*reg\.StatusCode\.Should\(\)\.Be\(HttpStatusCode\.OK\);\s*var tokenReq = new OAuth2TokenRequest\s*\{[^}]+\};\s*var tokenRes = await client\.PostAsJsonAsync\("/api/oauth2/token", tokenReq\);\s*tokenRes\.EnsureSuccessStatusCode\(\);\s*var tokenDto = await tokenRes\.Content\.ReadFromJsonAsync<OAuth2TokenResponse>\(\);\s*tokenDto\.Should\(\)\.NotBeNull\(\);\s*var access = tokenDto!\.AccessToken;',
        'var access = await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(client, _factory, email, "Test123!@#");',
        text,
        flags=re.MULTILINE,
    )
    text = re.sub(
        r'var reg = await client\.PostAsJsonAsync\("/api/oauth2/register", new \{ email, password = "Test123!@#"\ }\);\s*reg\.StatusCode\.Should\(\)\.Be\(HttpStatusCode\.OK\);\s*var tokenReq = new OAuth2TokenRequest\s*\{[^}]+\};\s*var tokenRes = await client\.PostAsJsonAsync\("/api/oauth2/token", tokenReq\);\s*tokenRes\.EnsureSuccessStatusCode\(\);\s*var access = \(await tokenRes\.Content\.ReadFromJsonAsync<OAuth2TokenResponse>\(\)\)!\.AccessToken!;',
        'var access = await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(client, _factory, email, "Test123!@#");',
        text,
        flags=re.MULTILINE,
    )

    # Simple await register before token flows
    text = re.sub(
        r'await _client\.PostAsJsonAsync\("/api/oauth2/register", new \{ email, password = "Test123!@#"\ }\);',
        'await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test123!@#");',
        text,
    )

    text = re.sub(
        r'await client\.PostAsJsonAsync\("/api/oauth2/register", new \{ email, password = "Test123!@#"\ }\);',
        'await IntegrationTestRegistration.CompleteRegistrationAsync(client, _factory, email, "Test123!@#");',
        text,
    )

    # registerResponse multiline
    text = re.sub(
        r'var registerResponse = await _client\.PostAsJsonAsync\("/api/oauth2/register", new \{ email, password = "Test123!@#", firstName = "([^"]+)", lastName = "([^"]+)" \}\);\s*registerResponse\.StatusCode\.Should\(\)\.Be\(HttpStatusCode\.OK\);',
        r'await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test123!@#", "\1", "\2");',
        text,
    )

    # Boundary: response from legacy register expecting status
    text = re.sub(
        r'var response = await _client\.PostAsJsonAsync\("/api/oauth2/register", new \{ email = \$"test_\{Guid\.NewGuid\(\)\}@test\.com", password = "([^"]+)" \}\);\s*response\.StatusCode\.Should\(\)\.Be\(HttpStatusCode\.(\w+)\);',
        r'var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(_client, _factory, $"test_{Guid.NewGuid()}@test.com", "\1");\n        status.Should().Be(HttpStatusCode.\2);',
        text,
    )

    text = re.sub(
        r'var response = await _client\.PostAsJsonAsync\("/api/oauth2/register", new \{ email = uniqueEmail, password = "Test123!@#"\ }\);\s*response\.StatusCode\.Should\(\)\.Be\(HttpStatusCode\.OK\);',
        'var status = await IntegrationTestRegistration.TryCompleteRegistrationAsync(_client, _factory, uniqueEmail, "Test123!@#");\n        status.Should().Be(HttpStatusCode.OK);',
        text,
    )

    if text != orig:
        path.write_text(text)
        return True
    return False


def main() -> None:
    for path in sorted(TESTS.glob("*.cs")):
        if path.name in SKIP_FILES:
            continue
        if '"/api/oauth2/register"' not in path.read_text():
            continue
        if patch_file(path):
            print("patched", path.name)


if __name__ == "__main__":
    main()
