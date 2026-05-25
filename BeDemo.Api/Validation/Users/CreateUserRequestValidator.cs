using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Users;

/// <summary>POST /api/users — <see cref="CreateUserModel"/> (§11.6).</summary>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserModel>
{
	public CreateUserRequestValidator()
	{
		RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(ValidationConstants.EmailMaxLength);
		RuleFor(x => x.Password)
			.NotEmpty()
			.MinimumLength(IdentityPasswordPolicyOptions.RecommendedMinimumLength)
			.WithErrorCode("val_password_min_length")
			.MaximumLength(ValidationConstants.PasswordMaxLength)
			.Must(v => !v.Contains('\0')).WithErrorCode("val_null_byte");
		RuleFor(x => x.FirstName).MaximumLength(ValidationConstants.NameMaxLength);
		RuleFor(x => x.LastName).MaximumLength(ValidationConstants.NameMaxLength);
	}
}
