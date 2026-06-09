using BeDemo.Api.Services.OperatorMail;
using BeDemo.Api.Services.OperatorPush;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace BeDemo.Api.Tests.Services;

/// <summary>
/// Characterization tests for the operator mail/push secret protectors (backend-refactor §4.5, 0 tests): a
/// protect → unprotect round-trip restores the plaintext, and the two protectors use DISTINCT Data-Protection
/// purposes so a mail ciphertext can never be decrypted by the push protector (and vice versa).
/// </summary>
public sealed class OperatorSecretProtectorTests
{
	private readonly IDataProtectionProvider _provider = new EphemeralDataProtectionProvider();

	[Fact]
	public void Mail_protect_unprotect_round_trips()
	{
		var p = new OperatorMailSecretProtector(_provider);
		var cipher = p.Protect("smtp-password!");
		cipher.Should().NotBe("smtp-password!");
		p.Unprotect(cipher).Should().Be("smtp-password!");
	}

	[Fact]
	public void Push_protect_unprotect_round_trips()
	{
		var p = new OperatorPushSecretProtector(_provider);
		var cipher = p.Protect("fcm-service-account");
		cipher.Should().NotBe("fcm-service-account");
		p.Unprotect(cipher).Should().Be("fcm-service-account");
	}

	[Fact]
	public void Mail_and_push_purposes_are_isolated()
	{
		var mail = new OperatorMailSecretProtector(_provider);
		var push = new OperatorPushSecretProtector(_provider);
		var mailCipher = mail.Protect("top-secret");

		// Cross-purpose decryption must fail — a leaked mail ciphertext is useless to the push protector.
		var crossDecrypt = () => push.Unprotect(mailCipher);
		crossDecrypt.Should().Throw<System.Security.Cryptography.CryptographicException>();
	}
}
