using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Services.OperatorMail;
using FluentValidation;
using Microsoft.Extensions.Hosting;

namespace BeDemo.Api.Validation.Admin;

public sealed class UpdateAdminMailSettingsValidator : AbstractValidator<UpdateAdminMailSettingsRequest>
{
	private static readonly HashSet<string> SupportedLocales = new(StringComparer.OrdinalIgnoreCase)
	{
		"en", "sk", "cs", "de", "fr", "it",
	};

	public UpdateAdminMailSettingsValidator(IHostEnvironment environment)
	{
		RuleFor(x => x.DefaultLocale)
			.NotEmpty()
			.Must(l => l != null && SupportedLocales.Contains(l.Trim()))
			.WithMessage("Unsupported default locale.");

		RuleFor(x => x.WorkerGrpcUrl)
			.Must(BeAbsoluteHttpUri)
			.When(x => x.Enabled && !string.IsNullOrWhiteSpace(x.WorkerGrpcUrl))
			.WithMessage("Worker gRPC URL must be an absolute http(s) URI.");

		RuleFor(x => x.WorkerGrpcUrl)
			.Must(url => url!.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			.When(x => x.Enabled && environment.IsProduction() && !string.IsNullOrWhiteSpace(x.WorkerGrpcUrl))
			.WithMessage("Production requires https worker URL.");

		When(x => x.RegistrationLinks != null, () =>
		{
			RuleFor(x => x.RegistrationLinks!.CompleteRegistrationPathTemplate)
				.Must(t => t != null && t.Contains("{locale}", StringComparison.Ordinal))
				.WithMessage("Registration path template must contain {locale}.");

			RuleFor(x => x.RegistrationLinks!.PortalPublicBaseUrl)
				.Must(BeAbsoluteHttpUri)
				.When(x => x.Enabled && !string.IsNullOrWhiteSpace(x.RegistrationLinks!.PortalPublicBaseUrl))
				.WithMessage("Portal public base URL must be absolute.");
		});

		When(x => x.Enabled, () =>
		{
			RuleFor(x => x.Smtp!.Host)
				.NotEmpty()
				.When(x => x.Smtp != null)
				.WithMessage("SMTP host is required when mail is enabled.");

			RuleFor(x => x.Smtp!.Port)
				.InclusiveBetween(1, 65535)
				.When(x => x.Smtp?.Port != null)
				.WithMessage("SMTP port out of range.");

			RuleFor(x => x.From!.Email)
				.NotEmpty()
				.EmailAddress()
				.When(x => x.From != null)
				.WithMessage("From email is required when mail is enabled.");
		});
	}

	private static bool BeAbsoluteHttpUri(string? value) =>
		!string.IsNullOrWhiteSpace(value) &&
		Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
		(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
