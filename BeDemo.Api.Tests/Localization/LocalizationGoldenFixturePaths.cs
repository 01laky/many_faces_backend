namespace BeDemo.Api.Tests.Localization;

/// <summary>
/// Resolves golden localization fixture files copied to the test output directory.
/// </summary>
internal static class LocalizationGoldenFixturePaths
{
	/// <summary>
	/// Portal English auth-flow subtree (login, register, core route slugs) used by §11.1 golden tests.
	/// </summary>
	public static string PortalAuthFlowGoldenEn =>
		Path.Combine(AppContext.BaseDirectory, "Fixtures", "portal-auth-flow-golden.en.json");
}
