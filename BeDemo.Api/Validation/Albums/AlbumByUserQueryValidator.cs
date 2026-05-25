using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Albums;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Albums.AlbumByUserQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class AlbumByUserQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Albums.AlbumByUserQuery>
{
	public AlbumByUserQueryValidator()
	{
		RuleFor(x => x.FaceId).OptionalPositiveFaceId();
	}
}
