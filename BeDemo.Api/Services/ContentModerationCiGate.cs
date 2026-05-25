namespace BeDemo.Api.Services;

/// <summary>
/// Security hardening v2 <b>PI-10</b>: canonical xUnit filter for required moderation security regression tests in CI.
/// </summary>
/// <remarks>
/// <para>
/// Parent monorepo job <c>many_faces_backend</c> runs full <c>dotnet test BeDemo.sln</c>, then an explicit gate via
/// <c>scripts/verify-moderation-security-tests.mjs</c> using <see cref="XunitFilterExpression"/> so prompt-injection /
/// corpus / trust-boundary failures surface with a clear step name (not only buried in the full suite log).
/// </para>
/// <para>
/// Test classes that participate must declare
/// <c>[Trait(<see cref="XunitTraitName"/>, <see cref="XunitTraitCategory"/>)]</c> — see
/// <see cref="RequiredSecurityTestClassNames"/> and <see cref="ContentModerationCiGateTests"/>.
/// </para>
/// </remarks>
public static class ContentModerationCiGate
{
	/// <summary>xUnit trait key used by <see cref="XunitFilterExpression"/>.</summary>
	public const string XunitTraitName = "Category";

	/// <summary>xUnit trait value for SHV2 moderation security regression tests (PI-10).</summary>
	public const string XunitTraitCategory = "ModerationSecurity";

	/// <summary>
	/// Filter passed to <c>dotnet test --filter</c> in CI and <c>verify-moderation-security-tests.mjs</c>.
	/// Keep in sync with the script header comment when changing.
	/// </summary>
	public const string XunitFilterExpression = $"{XunitTraitName}={XunitTraitCategory}";

	/// <summary>
	/// Human-readable list of test fixture types that must carry the PI-10 trait (enforced by unit test).
	/// </summary>
	public static readonly IReadOnlyList<string> RequiredSecurityTestClassNames =
	[
		"ContentModerationSecurityEdgeTests",
		"ContentModerationUnicodeSpoofingTests",
		"ContentModerationTrustBoundaryTests",
		"ContentModerationPayloadLogRedactionTests",
		"ContentModerationCiGateTests",
		"ContentModerationPreviewTextTests",
		"ContentModerationProductionPathTests",
	];
}
