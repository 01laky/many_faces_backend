using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Services.OperatorPush;
using FluentValidation;
using Microsoft.Extensions.Hosting;

namespace BeDemo.Api.Validation.Admin;

public sealed class UpdateAdminPushSettingsValidator : AbstractValidator<UpdateAdminPushSettingsRequest>
{
	private static readonly System.Text.RegularExpressions.Regex LocKeyPattern =
		new("^[a-zA-Z0-9_.-]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

	public UpdateAdminPushSettingsValidator(IHostEnvironment environment)
	{
		RuleFor(x => x.WorkerGrpcUrl)
			.Must(BeAbsoluteHttpUri)
			.When(x => x.Enabled && !string.IsNullOrWhiteSpace(x.WorkerGrpcUrl))
			.WithMessage("Worker gRPC URL must be an absolute http(s) URI.");

		RuleFor(x => x.WorkerGrpcUrl)
			.Must(url => url!.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			.When(x => x.Enabled && environment.IsProduction() && !string.IsNullOrWhiteSpace(x.WorkerGrpcUrl))
			.WithMessage("Production requires https worker URL.");

		RuleFor(x => x.GrpcDeadlineSeconds)
			.InclusiveBetween(1, 120)
			.When(x => x.GrpcDeadlineSeconds.HasValue)
			.WithMessage("gRPC deadline must be between 1 and 120 seconds.");

		When(x => x.Firebase?.ServiceAccountJson is { Length: > 0 }, () =>
		{
			RuleFor(x => x.Firebase!.ServiceAccountJson!)
				.Must(json => FirebaseServiceAccountValidator.TryValidate(json, out _, out _))
				.WithMessage("Invalid Firebase service account JSON.");
		});

		When(x => x.Enabled, () =>
		{
			RuleFor(x => x.WorkerGrpcUrl)
				.NotEmpty()
				.WithMessage("Worker gRPC URL is required when push is enabled.");

			RuleFor(x => x.Defaults!.TitleLocKey)
				.NotEmpty()
				.When(x => x.Defaults != null)
				.WithMessage("Default title loc key is required when push is enabled.");

			RuleFor(x => x.Defaults!.BodyLocKey)
				.NotEmpty()
				.When(x => x.Defaults != null)
				.WithMessage("Default body loc key is required when push is enabled.");

			RuleFor(x => x.Defaults!.TitleLocKey)
				.Must(k => k != null && LocKeyPattern.IsMatch(k))
				.When(x => !string.IsNullOrWhiteSpace(x.Defaults?.TitleLocKey))
				.WithMessage("Default title loc key has invalid characters.");

			RuleFor(x => x.Defaults!.BodyLocKey)
				.Must(k => k != null && LocKeyPattern.IsMatch(k))
				.When(x => !string.IsNullOrWhiteSpace(x.Defaults?.BodyLocKey))
				.WithMessage("Default body loc key has invalid characters.");
		});
	}

	internal static bool HasFirebaseAfterMerge(UpdateAdminPushSettingsRequest request, OperatorPushSettingsValues current)
	{
		var incoming = request.Firebase?.ServiceAccountJson;
		if (incoming is null)
			return current.HasFirebaseCredentials;

		if (incoming.Length == 0)
			return false;

		return FirebaseServiceAccountValidator.TryValidate(incoming, out _, out _);
	}

	private static bool BeAbsoluteHttpUri(string? value) =>
		!string.IsNullOrWhiteSpace(value) &&
		Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
		(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
