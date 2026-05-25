namespace BeDemo.Api.Services;

/// <summary>
/// xUnit filter expression for BSH3 backend security regression tests (CI: <c>verify-backend-security-tests.mjs</c>).
/// </summary>
public static class BackendSecurityCiGate
{
	/// <summary>Trait filter — keep aligned with monorepo CI script.</summary>
	public const string XunitFilterExpression = "Category=BackendSecurity";
}
