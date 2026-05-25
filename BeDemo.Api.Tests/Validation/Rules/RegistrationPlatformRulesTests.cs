using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class RegistrationPlatformRulesTests
{
	private sealed class Model { public string? Platform { get; set; } }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.Platform).RegistrationPlatform();
	}

	[Fact]
	public void T7_Invalid_platform_fails() => new Validator().TestValidate(new Model { Platform = "web" })
		.ShouldHaveValidationErrorFor(x => x.Platform);
}
