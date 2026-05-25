using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Albums;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Albums.UpdateAlbumCommentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateAlbumCommentRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Albums.UpdateAlbumCommentDto>
{
	public UpdateAlbumCommentRequestValidator()
	{
		RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);
	}
}
