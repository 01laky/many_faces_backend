using BeDemo.Api.Models.Requests.Admin;
using FluentValidation;

namespace BeDemo.Api.Validation.Admin;

public sealed class UpdateAdminMeProfileRequestValidator : AbstractValidator<UpdateAdminMeProfileRequest>
{
	public UpdateAdminMeProfileRequestValidator()
	{
		RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
		RuleFor(x => x.FirstName).MaximumLength(100).When(x => x.FirstName != null);
		RuleFor(x => x.LastName).MaximumLength(100).When(x => x.LastName != null);
		RuleFor(x => x.UserRoleId)
			.Null()
			.WithMessage("Global role cannot be changed via profile update");
	}
}
