using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.CreateSystemFaceChatRoomDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateSystemFaceChatRoomRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.CreateSystemFaceChatRoomDto>
{
	public CreateSystemFaceChatRoomRequestValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
	}
}
