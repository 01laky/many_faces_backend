using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.SetMyFaceRoleModel"/> (endpoint-schema-validation §12.1).</summary>
public sealed class SetMyFaceRoleRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.SetMyFaceRoleModel>
{
	public SetMyFaceRoleRequestValidator()
	{
		RuleFor(x => x.UserRoleId).GreaterThan(0);
	}
}
