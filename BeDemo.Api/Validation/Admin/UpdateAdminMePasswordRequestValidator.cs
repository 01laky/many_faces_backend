using BeDemo.Api.Configuration;
using BeDemo.Api.Models.Requests.Admin;
using FluentValidation;

namespace BeDemo.Api.Validation.Admin;

public sealed class UpdateAdminMePasswordRequestValidator : AbstractValidator<UpdateAdminMePasswordRequest>
{
    public UpdateAdminMePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(IdentityPasswordPolicyOptions.RecommendedMinimumLength);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword);
    }
}
