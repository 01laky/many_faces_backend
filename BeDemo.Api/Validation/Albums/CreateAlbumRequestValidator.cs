using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Albums;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Albums.CreateAlbumDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateAlbumRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Albums.CreateAlbumDto>
{
	public CreateAlbumRequestValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
	}
}
