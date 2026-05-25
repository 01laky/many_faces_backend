namespace BeDemo.Api.Tests;

/// <summary>
/// Shared credentials for integration tests after SHV2 <b>BE-A3</b> (minimum password length 12 in Testing).
/// </summary>
/// <remarks>
/// <c>Test1234!@##</c> is 12 characters and satisfies Identity complexity (digit, upper, lower, symbol).
/// </remarks>
internal static class IntegrationTestCredentials
{
	/// <summary>Default password for register/login flows in xUnit hosts (Testing environment).</summary>
	public const string DefaultPassword = "Test1234!@##";

	/// <summary>One character shorter than BE-A3 minimum — must fail registration in Testing.</summary>
	public const string PasswordOneBelowMinimum = "Test123!@#";
}
