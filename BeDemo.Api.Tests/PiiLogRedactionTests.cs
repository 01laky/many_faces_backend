using BeDemo.Api.Utils;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Unit tests for SHV2 BE-L1–L3 structured log redaction helpers.</summary>
public class PiiLogRedactionTests
{
    [Fact]
    public void FormatCredentialIdentifierForLog_DoesNotContainRawValue()
    {
        const string secret = "attacker@evil.com";
        var formatted = PiiLogRedaction.FormatCredentialIdentifierForLog(secret);
        formatted.Should().NotContain(secret);
        formatted.Should().Contain("credentialHintLength=");
        formatted.Should().Contain("credentialHintSha256Prefix=");
    }

    [Fact]
    public void FormatEmailForLog_MasksLocalPart_KeepsDomain()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var formatted = PiiLogRedaction.FormatEmailForLog("user@example.com", id);
        formatted.Should().NotContain("user@");
        formatted.Should().Contain("emailDomain=example.com");
        formatted.Should().Contain("inviteId=11111111");
    }

    [Fact]
    public void FormatChatMessageForLog_DoesNotContainRawMessage()
    {
        const string msg = "my secret prompt injection";
        var formatted = PiiLogRedaction.FormatChatMessageForLog(msg);
        formatted.Should().NotContain(msg);
        formatted.Should().Contain("messageLength=");
        formatted.Should().Contain("messageSha256Prefix=");
    }

    [Fact]
    public void ComputeSha256Prefix_IsStable()
    {
        PiiLogRedaction.ComputeSha256Prefix("abc").Should().Be(PiiLogRedaction.ComputeSha256Prefix("abc"));
    }
}
