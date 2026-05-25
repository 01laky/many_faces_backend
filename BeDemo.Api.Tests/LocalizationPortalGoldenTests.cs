using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using BeDemo.Api.Localization;
using BeDemo.Api.Services;
using BeDemo.Api.Tests.Localization;
using BeDemo.Api.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace BeDemo.Api.Tests;

/// <summary>
/// Golden-file regression for portal static localization (§11.1 centralized-static-i18n prompt).
/// </summary>
/// <remarks>
/// Locks <c>resources.en.common</c> auth-flow subtrees against
/// <see cref="LocalizationGoldenFixturePaths.PortalAuthFlowGoldenEn"/> so .resx → JSON export stays aligned
/// with the legacy portal <c>en.json</c> contract for login, register, and primary route slugs.
/// </remarks>
public class LocalizationPortalGoldenTests
{
	private static JsonObject GetPortalEnglishCommonNamespace()
	{
		var cache = new MemoryCache(new MemoryCacheOptions());
		var service = new LocalizationBundleService(
			cache,
			new HostEnvironmentStub(),
			NullLogger<LocalizationBundleService>.Instance);
		var bundle = service.GetBundle(LocalizationApp.Portal)
			?? throw new InvalidOperationException("Portal localization bundle must not be null.");

		if (!bundle.Resources.TryGetValue("en", out var enLang))
			throw new InvalidOperationException("Portal bundle must include resources.en.");

		if (!enLang.TryGetValue("common", out var commonObj))
			throw new InvalidOperationException("Portal bundle must include resources.en.common.");

		return commonObj;
	}

	/// <summary>
	/// Pure service path: exported subtree must match the committed golden file byte-for-byte in JSON structure.
	/// </summary>
	[Fact]
	public void PortalAuthFlowSubtree_FromBundleService_MatchesGoldenFile()
	{
		var common = GetPortalEnglishCommonNamespace();
		var actual = LocalizationGoldenSubtreeExtractor.ExtractPortalAuthFlow(common);

		Assert.True(File.Exists(LocalizationGoldenFixturePaths.PortalAuthFlowGoldenEn),
			$"Golden file missing at {LocalizationGoldenFixturePaths.PortalAuthFlowGoldenEn}. "
			+ "Run LocalizationPortalGoldenTests.RegeneratePortalAuthFlowGolden with REGENERATE_LOCALIZATION_GOLDEN=1.");

		var goldenJson = File.ReadAllText(LocalizationGoldenFixturePaths.PortalAuthFlowGoldenEn);
		var expected = LocalizationGoldenSubtreeExtractor.ParseGoldenFile(goldenJson);

		Assert.True(
			LocalizationGoldenSubtreeExtractor.DeepEquals(expected, actual, out var diff),
			diff);
	}

	/// <summary>
	/// HTTP path: <c>GET /api/localization/portal</c> must expose the same subtree as the bundle service (no drift in controller wiring).
	/// </summary>
	[Fact]
	public async Task PortalAuthFlowSubtree_FromHttpEndpoint_MatchesGoldenFile()
	{
		await using var factory = new CustomWebApplicationFactory<Program>();
		var client = factory.CreateClient();
		var response = await client.GetAsync("/api/localization/portal");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var json = await response.Content.ReadAsStringAsync();
		using var doc = JsonDocument.Parse(json);
		var commonElement = doc.RootElement
			.GetProperty("resources")
			.GetProperty("en")
			.GetProperty("common");

		var common = JsonNode.Parse(commonElement.GetRawText()) as JsonObject
			?? throw new InvalidOperationException("resources.en.common must be a JSON object.");

		var actual = LocalizationGoldenSubtreeExtractor.ExtractPortalAuthFlow(common);
		var goldenJson = File.ReadAllText(LocalizationGoldenFixturePaths.PortalAuthFlowGoldenEn);
		var expected = LocalizationGoldenSubtreeExtractor.ParseGoldenFile(goldenJson);

		Assert.True(
			LocalizationGoldenSubtreeExtractor.DeepEquals(expected, actual, out var diff),
			diff);
	}

	/// <summary>
	/// Guardrail: golden file must contain at least login title and register title (smoke for file corruption).
	/// </summary>
	[Fact]
	public void PortalAuthFlowGoldenFile_ContainsCoreLoginRegisterKeys()
	{
		var goldenJson = File.ReadAllText(LocalizationGoldenFixturePaths.PortalAuthFlowGoldenEn);
		var golden = LocalizationGoldenSubtreeExtractor.ParseGoldenFile(goldenJson);

		Assert.Equal("Login", golden["pages"]?["login"]?["title"]?.GetValue<string>());
		Assert.Equal("Register", golden["pages"]?["register"]?["title"]?.GetValue<string>());
		Assert.Equal("login", golden["routes"]?["login"]?.GetValue<string>());
		Assert.Equal("register", golden["routes"]?["register"]?.GetValue<string>());
		Assert.Equal("homepage", golden["routes"]?["homepage"]?.GetValue<string>());
	}

	/// <summary>
	/// Operator-only: rewrite golden file from current .resx export when copy changes are intentional.
	/// </summary>
	/// <remarks>
	/// <c>REGENERATE_LOCALIZATION_GOLDEN=1 dotnet test --filter RegeneratePortalAuthFlowGolden</c>
	/// </remarks>
	[Fact]
	public void RegeneratePortalAuthFlowGolden()
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("REGENERATE_LOCALIZATION_GOLDEN"), "1", StringComparison.Ordinal))
		{
			// Normal CI/local runs skip regeneration; golden file is the source of truth.
			return;
		}

		var common = GetPortalEnglishCommonNamespace();
		var subtree = LocalizationGoldenSubtreeExtractor.ExtractPortalAuthFlow(common);
		var canonical = LocalizationGoldenSubtreeExtractor.ToCanonicalJson(subtree);

		var repoFixturePath = Path.GetFullPath(Path.Combine(
			AppContext.BaseDirectory,
			"..", "..", "..",
			"Fixtures", "portal-auth-flow-golden.en.json"));

		Directory.CreateDirectory(Path.GetDirectoryName(repoFixturePath)!);
		File.WriteAllText(repoFixturePath, canonical.TrimEnd() + Environment.NewLine);
		Assert.True(File.Exists(repoFixturePath));
	}
}
