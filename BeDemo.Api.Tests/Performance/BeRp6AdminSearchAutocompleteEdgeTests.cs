using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using BeDemo.Api.Tests.Search;
using FluentAssertions;
using ManyFaces.Search.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP6 edge cases (BE-RP6-U1…U3).</summary>
public sealed class BeRp6AdminSearchAutocompleteEdgeTests
{
	private static (ServiceProvider Sp, FakeSearchQueryGateway Fake) BuildServices(
		FakeSearchQueryGateway fake,
		IFaceScopeContext faceScope)
	{
		var dbName = Guid.NewGuid().ToString("N");
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
		services.AddSingleton<ISearchQueryGateway>(fake);
		services.AddSingleton(faceScope);
		services.AddScoped<SearchHitBatchFilter>();
		services.AddSingleton<IOptions<SearchOptions>>(Options.Create(new SearchOptions
		{
			Enabled = true,
			WorkerGrpcUrl = "http://localhost:59996",
		}));
		services.AddSingleton<IOptions<PerformanceOptions>>(Options.Create(new PerformanceOptions()));
		services.AddMemoryCache();
		services.AddScoped<IAdminSearchAutocompleteService, AdminSearchAutocompleteService>();
		return (services.BuildServiceProvider(), fake);
	}

	/// <summary>BE-RP6-U1 — short query returns empty without worker autocomplete storm.</summary>
	[Fact]
	public async Task BE_RP6_U1_ShortQuery_NoWorkerCall()
	{
		var fake = new FakeSearchQueryGateway();
		var (sp, _) = BuildServices(fake, new PerformanceTestFaceScope { IsAdminFaceScope = true });
		await using (sp)
		{
			await using var svcScope = sp.CreateAsyncScope();
			var svc = svcScope.ServiceProvider.GetRequiredService<IAdminSearchAutocompleteService>();
			var result = await svc.SearchAsync("a", 0, 20, null, CancellationToken.None);
			result.Hits.Should().BeEmpty();
			fake.AutocompleteCallCount.Should().Be(0);
		}
	}

	/// <summary>BE-RP6-U2 — ACL filter removes hits for non-existent / removed entities.</summary>
	[Fact]
	public async Task BE_RP6_U2_AclFiltersForbiddenHits()
	{
		var fake = new FakeSearchQueryGateway
		{
			AutocompleteHandler = _ => new AutocompleteResponse
			{
				Hits =
				{
					new AutocompleteHit
					{
						DocumentType = SearchDocumentTypes.Album,
						EntityId = "999999",
						Title = "ghost",
						Score = 1,
					},
					new AutocompleteHit
					{
						DocumentType = SearchDocumentTypes.Face,
						EntityId = "1",
						Title = "face",
						Score = 0.5f,
					},
				},
				HasMore = false,
				NextOffset = 0,
			},
		};

		var (sp, _) = BuildServices(fake, new PerformanceTestFaceScope { IsAdminFaceScope = true });
		await using (sp)
		{
			await using var svcScope = sp.CreateAsyncScope();
			var db = svcScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			db.Faces.Add(new Face
			{
				Index = "visible",
				Title = "Visible",
				CreatedAt = DateTime.UtcNow,
				AllowRecensions = true,
				ChatRoomsCreate = true,
				VideoLoungesCreate = true,
			});
			await db.SaveChangesAsync();
			var faceId = (await db.Faces.FirstAsync()).Id.ToString();
			fake.AutocompleteHandler = _ => new AutocompleteResponse
			{
				Hits =
				{
					new AutocompleteHit { DocumentType = SearchDocumentTypes.Album, EntityId = "999999", Title = "ghost", Score = 1 },
					new AutocompleteHit { DocumentType = SearchDocumentTypes.Face, EntityId = faceId, Title = "face", Score = 0.5f },
				},
				HasMore = false,
			};

			var svc = svcScope.ServiceProvider.GetRequiredService<IAdminSearchAutocompleteService>();
			var result = await svc.SearchAsync("demo", 0, 20, null, CancellationToken.None);
			result.Hits.Should().ContainSingle(h => h.EntityType == SearchDocumentTypes.Face);
			result.Hits.Should().NotContain(h => h.EntityId == "999999");
		}
	}

	/// <summary>BE-RP6-U3 — page 2 offset returns distinct ids from page 1.</summary>
	[Fact]
	public async Task BE_RP6_U3_Page2_NoDuplicateIds()
	{
		var fake = new FakeSearchQueryGateway();
		var (sp, _) = BuildServices(fake, new PerformanceTestFaceScope { IsAdminFaceScope = true });
		await using (sp)
		{
			await using var svcScope = sp.CreateAsyncScope();
			var db = svcScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			for (var i = 0; i < 3; i++)
			{
				db.Faces.Add(new Face
				{
					Index = $"f{i}",
					Title = $"Face {i}",
					CreatedAt = DateTime.UtcNow,
					AllowRecensions = true,
					ChatRoomsCreate = true,
					VideoLoungesCreate = true,
				});
			}

			await db.SaveChangesAsync();
			var ids = await db.Faces.OrderBy(f => f.Id).Select(f => f.Id.ToString()).ToListAsync();

			fake.AutocompleteHandler = req =>
			{
				var slice = ids.Skip(req.Offset).Take(2).ToList();
				var response = new AutocompleteResponse
				{
					HasMore = req.Offset + slice.Count < ids.Count,
					NextOffset = req.Offset + slice.Count,
				};
				foreach (var id in slice)
				{
					response.Hits.Add(new AutocompleteHit
					{
						DocumentType = SearchDocumentTypes.Face,
						EntityId = id,
						Title = $"face-{id}",
						Score = 1,
					});
				}

				return response;
			};

			var svc = svcScope.ServiceProvider.GetRequiredService<IAdminSearchAutocompleteService>();
			var page1 = await svc.SearchAsync("face", 0, 2, null, CancellationToken.None);
			var page2 = await svc.SearchAsync("face", page1.NextOffset, 2, null, CancellationToken.None);

			var allIds = page1.Hits.Select(h => h.EntityId).Concat(page2.Hits.Select(h => h.EntityId)).ToList();
			allIds.Should().OnlyHaveUniqueItems();
			page1.Hits.Should().NotBeEmpty();
		}
	}
}
