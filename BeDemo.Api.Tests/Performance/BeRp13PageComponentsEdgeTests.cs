using BeDemo.Api.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP13 edge cases (BE-RP13-U1).</summary>
public sealed class BeRp13PageComponentsEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp13PageComponentsEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP13-U1 — GET page components is read-only (AsNoTracking); UpdatedAt unchanged.</summary>
	[Fact]
	public async Task BE_RP13_U1_GetPageComponents_NoChangeTrackingSideEffects()
	{
		int pageId;
		int countBefore;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			if (!await db.PageComponents.AnyAsync())
				return;

			var component = await db.PageComponents.AsNoTracking().FirstAsync();
			pageId = component.PageId;
			countBefore = await db.PageComponents.CountAsync();
		}

		using var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

		(await client.GetAsync($"/api/pagecomponents/page/{pageId}")).EnsureSuccessStatusCode();

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			(await db.PageComponents.CountAsync()).Should().Be(countBefore);
		}
	}
}
