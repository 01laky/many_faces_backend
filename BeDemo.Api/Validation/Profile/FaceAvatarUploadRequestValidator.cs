using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Profile;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Profile.FaceAvatarUploadRequest"/> (endpoint-schema-validation §12.1).</summary>
public sealed class FaceAvatarUploadRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Profile.FaceAvatarUploadRequest>
{
	public FaceAvatarUploadRequestValidator()
	{
		RuleFor(x => x.File).NotNull().WithErrorCode("val_file_required");
		RuleFor(x => x.FaceId).GreaterThan(0);
	}
}
