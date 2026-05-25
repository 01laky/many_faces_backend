using BeDemo.Api.Services;
using FluentValidation;

namespace BeDemo.Api.Validation.Rules;

/// <summary>Reusable FluentValidation rule extensions (endpoint-schema-validation §5).</summary>
public static class BeDemoValidationExtensions
{
	/// <summary>Rejects ASCII NUL in user-controlled strings (OAuth2 / registration hardening).</summary>
	public static IRuleBuilderOptions<T, string?> NoNullBytes<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
		ruleBuilder.Must(v => v == null || !v.Contains('\0'))
			.WithMessage("Value must not contain null bytes.")
			.WithErrorCode("val_null_byte");

	/// <summary>Absolute http/https URL only — wraps <see cref="ContentModerationHelpers.IsSafeHttpUrl"/>.</summary>
	public static IRuleBuilderOptions<T, string?> SafeHttpUrl<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
		ruleBuilder.Must(v => string.IsNullOrEmpty(v) || ContentModerationHelpers.IsSafeHttpUrl(v))
			.WithMessage("URL must be absolute http or https.")
			.WithErrorCode("val_url_unsafe");

	/// <summary>sortBy whitelist (case-insensitive) + safe token; sortDir asc|desc when sortBy set.</summary>
	public static void ApplyListSortRules<T>(
		this AbstractValidator<T> validator,
		System.Linq.Expressions.Expression<Func<T, string?>> sortBy,
		System.Linq.Expressions.Expression<Func<T, string?>> sortDir,
		params string[] whitelist)
	{
		validator.RuleFor(sortBy)
			.Must(v => SortRules.IsWhitelistedSortBy(v, whitelist))
			.WithMessage("sortBy is not allowed for this endpoint.")
			.WithErrorCode("val_sort_field_invalid");

		validator.RuleFor(sortDir)
			.Must(SortRules.IsValidSortDirection)
			.WithMessage("sortDir must be 'asc' or 'desc'.")
			.WithErrorCode("val_sort_dir_invalid");

		validator.RuleFor(x => x)
			.Must(m =>
			{
				var by = sortBy.Compile()(m);
				var dir = sortDir.Compile()(m);
				return string.IsNullOrWhiteSpace(by) || !string.IsNullOrWhiteSpace(dir);
			})
			.WithMessage("sortDir is required when sortBy is set.")
			.WithErrorCode("val_sort_dir_invalid");
	}

	/// <summary>page ≥ 1, pageSize in 1..100.</summary>
	public static void ApplyPaginationRules<T>(this AbstractValidator<T> validator,
		System.Linq.Expressions.Expression<Func<T, int>> page,
		System.Linq.Expressions.Expression<Func<T, int>> pageSize)
	{
		validator.RuleFor(page).GreaterThanOrEqualTo(1).WithErrorCode("val_page_min");
		validator.RuleFor(pageSize).InclusiveBetween(1, ValidationConstants.PageSizeDefaultMax)
			.WithErrorCode("val_page_size_range");
	}

	/// <summary>skip ≥ 0, take in 1..100 (admin invite list).</summary>
	public static void ApplySkipTakeRules<T>(this AbstractValidator<T> validator,
		System.Linq.Expressions.Expression<Func<T, int>> skip,
		System.Linq.Expressions.Expression<Func<T, int>> take)
	{
		validator.RuleFor(skip).GreaterThanOrEqualTo(0).WithErrorCode("val_skip_min");
		validator.RuleFor(take).InclusiveBetween(1, ValidationConstants.PageSizeDefaultMax)
			.WithErrorCode("val_take_range");
	}

	/// <summary>Email shape + max 256, trim implied by NotEmpty on required fields.</summary>
	public static IRuleBuilderOptions<T, string> EmailRule<T>(this IRuleBuilder<T, string> ruleBuilder) =>
		ruleBuilder.NotEmpty().EmailAddress().MaximumLength(ValidationConstants.EmailMaxLength)
			.WithErrorCode("val_email_invalid");

	public static IRuleBuilderOptions<T, string?> OptionalEmailRule<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
		ruleBuilder.Must(v => string.IsNullOrWhiteSpace(v) ||
							  (v.Length <= ValidationConstants.EmailMaxLength && v.Contains('@', StringComparison.Ordinal)))
			.WithMessage("Invalid email address.")
			.WithErrorCode("val_email_invalid");

	/// <summary>ASP.NET Identity user id: non-empty, no whitespace, max ~450.</summary>
	public static IRuleBuilderOptions<T, string> IdentityUserIdRule<T>(this IRuleBuilder<T, string> ruleBuilder) =>
		ruleBuilder.NotEmpty().MaximumLength(ValidationConstants.IdentityUserIdMaxLength)
			.Must(v => v.Trim().Length == v.Length && !v.Any(char.IsWhiteSpace))
			.WithMessage("User id must not contain whitespace.")
			.WithErrorCode("val_user_id_invalid");

	public static IRuleBuilderOptions<T, string?> OptionalIdentityUserIdRule<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
		ruleBuilder.MaximumLength(ValidationConstants.IdentityUserIdMaxLength)
			.Must(v => v == null || (v.Trim().Length == v.Length && !v.Any(char.IsWhiteSpace)))
			.WithMessage("User id must not contain whitespace.")
			.WithErrorCode("val_user_id_invalid");

	/// <summary>Null OK; if present after trim, apply max length.</summary>
	public static IRuleBuilderOptions<T, string?> OptionalTrimmedString<T>(
		this IRuleBuilder<T, string?> ruleBuilder,
		int maxLength) =>
		ruleBuilder.Must(v => v == null || v.Trim().Length <= maxLength)
			.WithMessage($"Value must be at most {maxLength} characters after trim.")
			.WithErrorCode("val_string_max_length");

	/// <summary>Enum must be defined (for nullable enums: null OK).</summary>
	public static IRuleBuilderOptions<T, TEnum?> EnumDefinedRule<T, TEnum>(this IRuleBuilder<T, TEnum?> ruleBuilder)
		where TEnum : struct, Enum =>
		ruleBuilder.Must(v => !v.HasValue || Enum.IsDefined(v.Value))
			.WithMessage("Invalid enum value.")
			.WithErrorCode("val_enum_invalid");

	/// <summary>faceId query parameter when present must be &gt; 0.</summary>
	public static IRuleBuilderOptions<T, int?> OptionalPositiveFaceId<T>(this IRuleBuilder<T, int?> ruleBuilder) =>
		ruleBuilder.Must(v => v == null || v > 0)
			.WithMessage("faceId must be greater than zero when provided.")
			.WithErrorCode("val_face_id_invalid");

	public static IRuleBuilderOptions<T, int> PositiveFaceId<T>(this IRuleBuilder<T, int> ruleBuilder) =>
		ruleBuilder.GreaterThan(0).WithErrorCode("val_face_id_invalid");

	/// <summary>AI confidence 0.0–1.0 when set.</summary>
	public static IRuleBuilderOptions<T, double?> ConfidenceRangeRule<T>(this IRuleBuilder<T, double?> ruleBuilder) =>
		ruleBuilder.Must(c => !c.HasValue || (c >= 0 && c <= 1))
			.WithMessage("Confidence must be between 0 and 1.")
			.WithErrorCode("val_confidence_range");

	/// <summary>UTC window: from ≤ to, span ≤ maxDays.</summary>
	public static void ApplyUtcRangeRules<T>(this AbstractValidator<T> validator,
		System.Linq.Expressions.Expression<Func<T, DateTime>> fromUtc,
		System.Linq.Expressions.Expression<Func<T, DateTime>> toUtc,
		int maxSpanDays = ValidationConstants.StatsMaxSpanDays)
	{
		validator.RuleFor(toUtc).GreaterThanOrEqualTo(fromUtc).WithErrorCode("val_utc_range");
		validator.RuleFor(x => x).Must(m =>
		{
			var from = fromUtc.Compile()(m);
			var to = toUtc.Compile()(m);
			return (to - from).TotalDays <= maxSpanDays;
		}).WithMessage($"Range must not exceed {maxSpanDays} days.").WithErrorCode("val_utc_span");
	}

	/// <summary>Registration mail platform: only <c>mobile</c> or empty.</summary>
	public static IRuleBuilderOptions<T, string?> RegistrationPlatform<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
		ruleBuilder.Must(v => string.IsNullOrWhiteSpace(v) ||
							 string.Equals(v, "mobile", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Platform must be 'mobile' when provided.")
			.WithErrorCode("val_platform_invalid");

	/// <summary>Push device platform: ios or android.</summary>
	public static IRuleBuilderOptions<T, string> PushPlatform<T>(this IRuleBuilder<T, string> ruleBuilder) =>
		ruleBuilder.Must(v => v.Equals("ios", StringComparison.OrdinalIgnoreCase) ||
							  v.Equals("android", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Platform must be 'ios' or 'android'.")
			.WithErrorCode("val_push_platform_invalid");

	/// <summary>Bounded list of safe HTTP(S) image URLs.</summary>
	public static IRuleBuilderOptions<T, IEnumerable<string>?> ImageUrlListRule<T>(
		this IRuleBuilder<T, IEnumerable<string>?> ruleBuilder,
		int maxCount = ValidationConstants.MaxBlogImages,
		int maxUrlLength = ValidationConstants.ImageUrlMaxLength) =>
		ruleBuilder.Must(urls => urls == null || (urls.Count() <= maxCount && urls.All(u =>
			!string.IsNullOrWhiteSpace(u) && u.Length <= maxUrlLength && ContentModerationHelpers.IsSafeHttpUrl(u))))
			.WithMessage($"At most {maxCount} safe http(s) image URLs, each max {maxUrlLength} chars.")
			.WithErrorCode("val_image_url_list");

	/// <summary>CMS page path: leading slash, no parent-segment traversal.</summary>
	public static IRuleBuilderOptions<T, string> SlugPathRule<T>(this IRuleBuilder<T, string> ruleBuilder) =>
		ruleBuilder.NotEmpty().MaximumLength(ValidationConstants.PagePathMaxLength)
			.Must(p => p.StartsWith('/') && !p.Contains("..", StringComparison.Ordinal))
			.WithMessage("Path must start with '/' and must not contain '..'.")
			.WithErrorCode("val_slug_path");

	public static IRuleBuilderOptions<T, string?> OptionalSlugPathRule<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
		ruleBuilder.MaximumLength(ValidationConstants.PagePathMaxLength)
			.Must(p => p == null || (p.StartsWith('/') && !p.Contains("..", StringComparison.Ordinal)))
			.WithMessage("Path must start with '/' and must not contain '..'.")
			.WithErrorCode("val_slug_path");

	/// <summary>GridSchema JSON string max length.</summary>
	public static IRuleBuilderOptions<T, string?> GridSchemaJsonRule<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
		ruleBuilder.MaximumLength(ValidationConstants.GridSchemaMaxLength)
			.WithErrorCode("val_grid_schema_max");
}
