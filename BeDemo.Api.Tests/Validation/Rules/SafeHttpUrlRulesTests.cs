using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class SafeHttpUrlRulesTests
{
	private sealed class Model { public string? Url { get; set; } }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.Url).SafeHttpUrl();
	}

	[Fact]
	public void T5_Unsafe_url_fails() => new Validator().TestValidate(new Model { Url = "javascript:alert(1)" })
		.ShouldHaveValidationErrorFor(x => x.Url);

	[Fact]
	public void T11_Https_ok() => new Validator().TestValidate(new Model { Url = "https://example.com/x" })
		.ShouldNotHaveAnyValidationErrors();
}
