using BeDemo.Api.Security;
using FluentAssertions;

namespace BeDemo.Api.Tests;

public sealed class TokenHasherTests
{
	[Fact]
	public void Sha256Hex_ShouldBeDeterministic()
	{
		TokenHasher.Sha256Hex("same-input").Should().Be(TokenHasher.Sha256Hex("same-input"));
	}

	[Fact]
	public void Sha256Hex_ShouldDiffer_ForDifferentInputs()
	{
		TokenHasher.Sha256Hex("a").Should().NotBe(TokenHasher.Sha256Hex("b"));
	}

	[Fact]
	public void Sha256Hex_ShouldReturnLowercaseHex64Chars()
	{
		var hash = TokenHasher.Sha256Hex("refresh-token-plaintext");
		hash.Should().MatchRegex("^[a-f0-9]{64}$");
	}

	[Fact]
	public void Sha256Hex_ShouldHashEmptyString()
	{
		TokenHasher.Sha256Hex("")
			.Should()
			.Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
	}
}
