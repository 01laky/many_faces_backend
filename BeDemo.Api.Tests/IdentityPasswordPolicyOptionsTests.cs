using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;

namespace BeDemo.Api.Tests;

/// <summary>SHV2 BE-A3: Identity minimum password length configuration and startup validation.</summary>
public sealed class IdentityPasswordPolicyOptionsTests
{
	[Fact]
	public void RecommendedMinimumLength_is_twelve()
	{
		IdentityPasswordPolicyOptions.RecommendedMinimumLength.Should().Be(12);
	}

	[Fact]
	public void BeA3_validation_rejects_sub_twelve_outside_development()
	{
		var validator = new IdentityPasswordPolicyValidateOptions(
			new HostEnvironment { EnvironmentName = Environments.Production });

		var result = validator.Validate(
			Options.DefaultName,
			new IdentityPasswordPolicyOptions { RequiredLength = IdentityPasswordPolicyOptions.LegacyWeakMinimumLength });

		result.Failed.Should().BeTrue();
	}

	[Fact]
	public void BeA3_validation_allows_legacy_four_only_in_development()
	{
		var validator = new IdentityPasswordPolicyValidateOptions(
			new HostEnvironment { EnvironmentName = Environments.Development });

		var result = validator.Validate(
			Options.DefaultName,
			new IdentityPasswordPolicyOptions { RequiredLength = IdentityPasswordPolicyOptions.LegacyWeakMinimumLength });

		result.Succeeded.Should().BeTrue();
	}

	[Fact]
	public void Default_appsettings_json_requires_twelve_character_passwords()
	{
		var config = new ConfigurationBuilder()
			.SetBasePath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BeDemo.Api"))
			.AddJsonFile("appsettings.json", optional: false)
			.Build();

		config.GetValue<int>($"{IdentityPasswordPolicyOptions.SectionName}:RequiredLength")
			.Should()
			.Be(IdentityPasswordPolicyOptions.RecommendedMinimumLength);
	}

	[Fact]
	public void Development_appsettings_may_lower_required_length_for_local_demo()
	{
		var config = new ConfigurationBuilder()
			.SetBasePath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BeDemo.Api"))
			.AddJsonFile("appsettings.json", optional: false)
			.AddJsonFile("appsettings.Development.json", optional: false)
			.Build();

		config.GetValue<int>($"{IdentityPasswordPolicyOptions.SectionName}:RequiredLength")
			.Should()
			.Be(IdentityPasswordPolicyOptions.LegacyWeakMinimumLength);
	}

	[Fact]
	public void Startup_fails_when_production_profile_uses_legacy_four_character_minimum()
	{
		var act = () =>
		{
			using var factory = new LegacyPasswordPolicyWebFactory();
			factory.CreateClient();
		};

		act.Should().Throw<Exception>();
	}

	/// <summary>Simulates mis-merged Production config with pre-BE-A3 minimum length.</summary>
	private sealed class LegacyPasswordPolicyWebFactory : CustomWebApplicationFactory<Program>
	{
		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			base.ConfigureWebHost(builder);
			builder.UseEnvironment(Environments.Production);
			builder.ConfigureAppConfiguration((_, config) =>
			{
				config.AddInMemoryCollection(new Dictionary<string, string?>
				{
					[$"{IdentityPasswordPolicyOptions.SectionName}:RequiredLength"] =
						IdentityPasswordPolicyOptions.LegacyWeakMinimumLength.ToString(),
				});
			});
		}
	}

	private sealed class HostEnvironment : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = Environments.Production;
		public string ApplicationName { get; set; } = "BeDemo.Api.Tests";
		public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
		public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
			new Microsoft.Extensions.FileProviders.NullFileProvider();
	}
}
