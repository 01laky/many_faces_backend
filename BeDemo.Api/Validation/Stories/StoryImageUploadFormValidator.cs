using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Stories;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.DTOs.StoryImageUploadForm"/> (endpoint-schema-validation §12.1).</summary>
public sealed class StoryImageUploadFormValidator : AbstractValidator<BeDemo.Api.Models.DTOs.StoryImageUploadForm>
{
	public StoryImageUploadFormValidator()
	{
		RuleFor(x => x.File).NotNull().WithErrorCode("val_file_required");
		When(x => x.File is not null, () =>
		{
			RuleFor(x => x.File!.ContentType)
				.Must(ct => ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
				.WithErrorCode("val_file_content_type");
		});
		RuleFor(x => x.SortOrder).InclusiveBetween(0, 9).WithErrorCode("val_sort_order_range");
		RuleFor(x => x.Description).MaximumLength(ValidationConstants.DescriptionMediumMaxLength).When(x => x.Description != null);
	}
}
