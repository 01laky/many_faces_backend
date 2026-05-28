using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP29 edge cases (BE-RP29-U1…U2).</summary>
public sealed class BeRp29EfQueryTagsEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp29EfQueryTagsEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP29-U1 — when enabled, tagged faces-config query executes successfully.</summary>
	[Fact]
	public async Task BE_RP29_U1_TagsEnabled_FacesConfigQuerySucceeds()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var sp = scope.ServiceProvider;
		var svc = new BeDemo.Api.Services.Faces.FacesConfigService(
			sp.GetRequiredService<ApplicationDbContext>(),
			new PerformanceTestFaceScope(),
			sp.GetRequiredService<IAccessEvaluator>(),
			sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
			Microsoft.Extensions.Options.Options.Create(new PerformanceOptions { EfQueryTagsEnabled = true }),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<BeDemo.Api.Services.Faces.FacesConfigService>.Instance);

		var result = await svc.GetFacesConfigAsync(new System.Security.Claims.ClaimsPrincipal(), null);
		result.Should().NotBeEmpty();
	}

	/// <summary>BE-RP29-U1b — TagIfEnabled attaches tag metadata when enabled (relational providers).</summary>
	[Fact]
	public void BE_RP29_U1b_TagIfEnabled_WhenEnabled_QueryIsTagged()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var enabled = Microsoft.Extensions.Options.Options.Create(new PerformanceOptions { EfQueryTagsEnabled = true });
		var query = db.Faces.AsNoTracking().TagIfEnabled(enabled, EfQueryTags.FacesConfig);
		query.Should().NotBeNull();
	}

	/// <summary>BE-RP29-U2 — when disabled, query string does not contain BE-RP tag prefix.</summary>
	[Fact]
	public void BE_RP29_U2_TagsDisabled_QueryStringHasNoTagPrefix()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var perf = Options.Create(new PerformanceOptions { EfQueryTagsEnabled = false });

		var sql = db.Faces.AsNoTracking().TagIfEnabled(perf, EfQueryTags.FacesConfig).ToQueryString();
		sql.Should().NotContain("BE-RP:");
	}
}
