using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Albums;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Albums.UpdateAlbumDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateAlbumRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Albums.UpdateAlbumDto>
{
	public UpdateAlbumRequestValidator()
	{
		RuleFor(x => x.Title).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Title != null);
	}
}
