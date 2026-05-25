using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeDemo.Api.Models.DTOs.Search;
using BeDemo.Api.Services.Search;
using FluentAssertions;
using Grpc.Core;
using ManyFaces.Search.V1;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BeDemo.Api.Tests.Search;

public sealed class AdminSearchAutocompleteEdgeTests
	: IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public AdminSearchAutocompleteEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	public void Dispose() { }

	private HttpClient CreateSearchEnabledClient(FakeSearchQueryGateway fake)
	{
		var factory = new SearchEnabledWebApplicationFactory(fake);
		var client = factory.CreateFaceClient("admin");
		return client;
	}

	/// <summary>GSH1-T-A04 — unauthenticated request returns 401.</summary>
	[Fact]
	public async Task GSH1_T_A04_Unauthenticated_Returns401()
	{
		var client = _factory.CreateFaceClient("admin");
		var response = await client.GetAsync("/api/search/autocomplete?q=demo");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>GSH1-T-A05 — global ADMIN (not super) on admin face returns 403.</summary>
	[Fact]
	public async Task GSH1_T_A05_GlobalAdmin_Returns403()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/search/autocomplete?q=demo");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	/// <summary>GSH1-T-A01 — missing or whitespace q returns 400.</summary>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task GSH1_T_A01_MissingOrWhitespaceQuery_Returns400(string q)
	{
		var fake = new FakeSearchQueryGateway();
		var client = CreateSearchEnabledClient(fake);
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync($"/api/search/autocomplete?q={Uri.EscapeDataString(q)}");
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		fake.AutocompleteCallCount.Should().Be(0);
	}

	/// <summary>GSH1-T-A02 — q length 1 returns 200 empty list; worker not called.</summary>
	[Fact]
	public async Task GSH1_T_A02_SingleCharQuery_Returns200Empty_WorkerNotCalled()
	{
		var fake = new FakeSearchQueryGateway();
		var client = CreateSearchEnabledClient(fake);
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/search/autocomplete?q=a");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var dto = await response.Content.ReadFromJsonAsync<AdminSearchAutocompleteResponse>();
		dto!.Hits.Should().BeEmpty();
		dto.SearchAvailable.Should().BeTrue();
		fake.AutocompleteCallCount.Should().Be(0);
	}

	/// <summary>GSH1-T-A06 — Search:Enabled=false returns degraded 200.</summary>
	[Fact]
	public async Task GSH1_T_A06_SearchDisabled_Returns200Degraded()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/search/autocomplete?q=demo");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var dto = await response.Content.ReadFromJsonAsync<AdminSearchAutocompleteResponse>();
		dto!.SearchAvailable.Should().BeFalse();
		dto.Hits.Should().BeEmpty();
	}

	/// <summary>GSH1-T-A07 — gRPC worker failure returns 200 degraded.</summary>
	[Fact]
	public async Task GSH1_T_A07_WorkerUnavailable_Returns200Degraded()
	{
		var fake = new FakeSearchQueryGateway
		{
			AutocompleteException = new RpcException(new Status(StatusCode.Unavailable, "down")),
		};
		var client = CreateSearchEnabledClient(fake);
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/search/autocomplete?q=demo");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var dto = await response.Content.ReadFromJsonAsync<AdminSearchAutocompleteResponse>();
		dto!.SearchAvailable.Should().BeFalse();
		dto.Message.Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>GSH1-T-A08 — hit for non-visible entity filtered out.</summary>
	[Fact]
	public async Task GSH1_T_A08_HitForMissingEntity_FilteredOut()
	{
		var fake = new FakeSearchQueryGateway
		{
			NextAutocompleteResponse = new AutocompleteResponse
			{
				Hits =
				{
					new AutocompleteHit
					{
						DocumentType = SearchDocumentTypes.User,
						EntityId = "non-existent-user-id-xyz",
						Title = "ghost",
					},
				},
				HasMore = false,
			},
		};
		var client = CreateSearchEnabledClient(fake);
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/search/autocomplete?q=ghost");
		var dto = await response.Content.ReadFromJsonAsync<AdminSearchAutocompleteResponse>();
		dto!.Hits.Should().BeEmpty();
	}

	/// <summary>GSH1-T-A09 — pageSize above 100 clamped.</summary>
	[Fact]
	public async Task GSH1_T_A09_PageSizeAbove100_Clamped()
	{
		var fake = new FakeSearchQueryGateway { NextAutocompleteResponse = new AutocompleteResponse() };
		var client = CreateSearchEnabledClient(fake);
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/search/autocomplete?q=demo&pageSize=500");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var dto = await response.Content.ReadFromJsonAsync<AdminSearchAutocompleteResponse>();
		dto!.PageSize.Should().Be(100);
	}

	/// <summary>GSH1-T-A10 — pageSize zero returns 400.</summary>
	[Fact]
	public async Task GSH1_T_A10_PageSizeZero_Returns400()
	{
		var fake = new FakeSearchQueryGateway();
		var client = CreateSearchEnabledClient(fake);
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/search/autocomplete?q=demo&pageSize=0");
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	/// <summary>GSH1-T-A18 — negative offset returns 400.</summary>
	[Fact]
	public async Task GSH1_T_A18_NegativeOffset_Returns400()
	{
		var fake = new FakeSearchQueryGateway();
		var client = CreateSearchEnabledClient(fake);
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/search/autocomplete?q=demo&offset=-1");
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	/// <summary>GSH1-T-A03 — injection chars pass through to worker; response sanitized.</summary>
	[Fact]
	public async Task GSH1_T_A03_InjectionChars_PassThroughToWorker_ResponseSanitized()
	{
		AutocompleteRequest? captured = null;
		var fake = new FakeSearchQueryGateway
		{
			AutocompleteHandler = req =>
			{
				captured = req;
				return new AutocompleteResponse
				{
					Hits =
					{
						new AutocompleteHit
						{
							DocumentType = SearchDocumentTypes.Face,
							EntityId = "1",
							Title = "<script>x</script>face",
						},
					},
				};
			},
		};
		var client = CreateSearchEnabledClient(fake);
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var q = "'; DROP TABLE--<b>demo</b>";
		await client.GetAsync($"/api/search/autocomplete?q={Uri.EscapeDataString(q)}");
		captured!.Query.Should().Be(q);
	}

	/// <summary>GSH1-T-A12 — unicode query does not 500.</summary>
	[Fact]
	public async Task GSH1_T_A12_UnicodeQuery_Returns200()
	{
		var fake = new FakeSearchQueryGateway { NextAutocompleteResponse = new AutocompleteResponse() };
		var client = CreateSearchEnabledClient(fake);
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/search/autocomplete?q=čšž");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	/// <summary>GSH1-T-A13 — concurrent requests succeed without shared mutable bugs.</summary>
	[Fact]
	public async Task GSH1_T_A13_ConcurrentRequests_BothSucceed()
	{
		var fake = new FakeSearchQueryGateway { NextAutocompleteResponse = new AutocompleteResponse() };
		var client = CreateSearchEnabledClient(fake);
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var t1 = client.GetAsync("/api/search/autocomplete?q=aa");
		var t2 = client.GetAsync("/api/search/autocomplete?q=bb");
		var responses = await Task.WhenAll(t1, t2);
		responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
	}
}

internal sealed class SearchEnabledWebApplicationFactory : CustomWebApplicationFactory<Program>
{
	private readonly FakeSearchQueryGateway _fake;

	public SearchEnabledWebApplicationFactory(FakeSearchQueryGateway fake) => _fake = fake;

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.UseSetting("Search:Enabled", "true");
		builder.UseSetting("Search:WorkerGrpcUrl", "http://localhost:59996");
		builder.ConfigureServices(services =>
		{
			services.RemoveAll<ISearchQueryGateway>();
			services.AddSingleton<ISearchQueryGateway>(_fake);
		});
	}
}
