using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Blogs;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Blogs.CreateBlogDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateBlogRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Blogs.CreateBlogDto>
{
    public CreateBlogRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength).WithErrorCode("val_string_required");
        RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.BlogContentMaxLength).WithErrorCode("val_string_required");
        RuleFor(x => x.FaceId).GreaterThan(0).WithErrorCode("val_face_id_invalid");
    }
}
