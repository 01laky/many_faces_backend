using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.OAuth;

/// <summary>POST /api/oauth2/token — <see cref="OAuth2TokenRequest"/> (§11.2).</summary>
public sealed class OAuth2TokenRequestValidator : AbstractValidator<OAuth2TokenRequest>
{
	private static readonly string[] AllowedGrantTypes = ["password", "refresh_token"];

	public OAuth2TokenRequestValidator()
	{
		RuleFor(x => x.GrantType)
			.NotEmpty()
			.WithErrorCode("val_grant_type_required")
			.Must(g => AllowedGrantTypes.Contains(g, StringComparer.OrdinalIgnoreCase))
			.WithMessage("Grant type must be 'password' or 'refresh_token'.")
			.WithErrorCode("val_grant_type_invalid");

		RuleFor(x => x.ClientId)
			.MaximumLength(ValidationConstants.OAuthClientFieldMaxLength)
			.When(x => !string.IsNullOrEmpty(x.ClientId));

		RuleFor(x => x.ClientSecret)
			.MaximumLength(ValidationConstants.OAuthClientFieldMaxLength)
			.When(x => !string.IsNullOrEmpty(x.ClientSecret));

		RuleFor(x => x.Username).NoNullBytes();
		RuleFor(x => x.Password).NoNullBytes();
		RuleFor(x => x.RefreshToken).NoNullBytes();

		When(x => string.Equals(x.GrantType, "password", StringComparison.OrdinalIgnoreCase), () =>
		{
			RuleFor(x => x.Username)
				.NotEmpty()
				.WithErrorCode("val_username_required");
			RuleFor(x => x.Password)
				.NotEmpty()
				.WithErrorCode("val_password_required");
		});

		When(x => string.Equals(x.GrantType, "refresh_token", StringComparison.OrdinalIgnoreCase), () =>
		{
			RuleFor(x => x.RefreshToken)
				.NotEmpty()
				.WithErrorCode("val_refresh_token_required");
		});

		RuleFor(x => x.Signature)
			.MaximumLength(2048)
			.When(x => !string.IsNullOrEmpty(x.Signature));

		RuleFor(x => x.SignatureAlgorithm)
			.MaximumLength(32)
			.When(x => !string.IsNullOrEmpty(x.SignatureAlgorithm));
	}
}
