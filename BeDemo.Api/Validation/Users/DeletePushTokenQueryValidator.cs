using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Users;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Users.DeletePushTokenQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class DeletePushTokenQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Users.DeletePushTokenQuery>
{
	public DeletePushTokenQueryValidator()
	{
		RuleFor(x => x.InstallationId).MaximumLength(ValidationConstants.InstallationIdMaxLength)
			.When(x => !string.IsNullOrEmpty(x.InstallationId));
	}
}
