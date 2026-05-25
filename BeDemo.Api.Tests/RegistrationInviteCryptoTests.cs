using BeDemo.Api.Services;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class RegistrationInviteCryptoTests
{
	[Fact]
	public void HashCode_ShouldMatch_ForSameInput()
	{
		var a = RegistrationInviteCrypto.HashCode("abc123", "pepper");
		var b = RegistrationInviteCrypto.HashCode("abc123", "pepper");
		RegistrationInviteCrypto.FixedTimeEqualsHash(a, b).Should().BeTrue();
	}

	[Fact]
	public void GenerateLinkHash_ShouldBeUnique()
	{
		var a = RegistrationInviteCrypto.GenerateLinkHash();
		var b = RegistrationInviteCrypto.GenerateLinkHash();
		a.Should().NotBe(b);
		a.Length.Should().BeGreaterThan(20);
	}
}
