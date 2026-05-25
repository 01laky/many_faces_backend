using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class ImageUrlListRulesTests
{
	private sealed class Model { public List<string>? Urls { get; set; } }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.Urls).ImageUrlListRule(maxCount: 2);
	}

	[Fact]
	public void T9_Too_many_urls_fails() => new Validator().TestValidate(new Model
	{
		Urls = ["https://a.com", "https://b.com", "https://c.com"],
	}).ShouldHaveValidationErrorFor(x => x.Urls);
}
