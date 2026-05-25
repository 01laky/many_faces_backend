namespace BeDemo.Api.Configuration;

/// <summary>
/// ASP.NET Core Identity minimum password length bound from configuration (SHV2 <b>BE-A3</b>).
/// </summary>
/// <remarks>
/// <para>
/// Production and Testing hosts must use at least <see cref="RecommendedMinimumLength"/> (12).
/// Only the <b>Development</b> environment may lower <see cref="RequiredLength"/> via
/// <c>appsettings.Development.json</c> for faster local manual testing.
/// </para>
/// <para>
/// Other complexity rules (digit, upper, lower, non-alphanumeric) remain configured in <c>Program.cs</c>
/// alongside <see cref="Microsoft.AspNetCore.Identity.IdentityOptions.Password"/>.
/// </para>
/// </remarks>
public sealed class IdentityPasswordPolicyOptions
{
	/// <summary>Configuration section (<c>appsettings.json</c> → <c>Identity:Password</c>).</summary>
	public const string SectionName = "Identity:Password";

	/// <summary>SHV2 BE-A3 policy minimum for non-Development environments.</summary>
	public const int RecommendedMinimumLength = 12;

	/// <summary>Legacy demo minimum before BE-A3 — referenced in validation messages and tests only.</summary>
	public const int LegacyWeakMinimumLength = 4;

	/// <summary>
	/// Minimum password length enforced by Identity on register, password reset, and admin user creation.
	/// </summary>
	public int RequiredLength { get; set; } = RecommendedMinimumLength;
}
