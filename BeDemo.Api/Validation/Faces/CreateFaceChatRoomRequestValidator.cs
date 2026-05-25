using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.CreateFaceChatRoomDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateFaceChatRoomRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.CreateFaceChatRoomDto>
{
	public CreateFaceChatRoomRequestValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
	}
}
