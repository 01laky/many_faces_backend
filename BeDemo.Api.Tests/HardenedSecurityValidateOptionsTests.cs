using BeDemo.Api.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests;

[Trait("Category", "BackendSecurity")]
public sealed class HardenedSecurityValidateOptionsTests
{
	[Fact]
	public void Validate_succeeds_outside_Hardened_environment()
	{
		var env = new Mock<IWebHostEnvironment>();
		env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();

		var validator = new HardenedSecurityValidateOptions(env.Object, config);
		var result = validator.Validate(null, new HardenedSecurityOptions());

		result.Succeeded.Should().BeTrue();
	}

	[Fact]
	public void Validate_fails_in_Hardened_when_placeholders_remain()
	{
		var env = new Mock<IWebHostEnvironment>();
		env.Setup(e => e.EnvironmentName).Returns("Hardened");

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Uploads:SigningSecret"] = "CHANGE_ME-upload-signing-secret-32chars!!",
				["RegistrationInvite:HmacPepper"] = "CHANGE_ME-registration-pepper-fixed!!",
				["OAuth2:ClientSecret"] = "CHANGE_ME-oauth-client-secret",
				["Search:Enabled"] = "true",
				["Search:WorkerGrpcUrl"] = "http://search:50051",
				["Search:WorkerAuthToken"] = "",
				["AiService:GrpcAddress"] = "http://ai:50051",
			})
			.Build();

		var validator = new HardenedSecurityValidateOptions(env.Object, config);
		var result = validator.Validate(
			null,
			new HardenedSecurityOptions
			{
				RejectPlaceholderSecrets = true,
				EnforceWorkerTlsAndTokens = true,
			});

		result.Failed.Should().BeTrue();
		result.FailureMessage.Should().Contain("Uploads:SigningSecret");
		result.FailureMessage.Should().Contain("Search:WorkerGrpcUrl must use https://");
		result.FailureMessage.Should().Contain("AiService:GrpcAddress must use https://");
	}
}
