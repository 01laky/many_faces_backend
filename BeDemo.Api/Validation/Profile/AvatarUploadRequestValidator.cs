using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Profile;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Profile.AvatarUploadRequest"/> (endpoint-schema-validation §12.1).</summary>
public sealed class AvatarUploadRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Profile.AvatarUploadRequest>
{
	public AvatarUploadRequestValidator()
	{
		RuleFor(x => x.File).NotNull().WithErrorCode("val_file_required");
	}
}
