using BeDemo.Api.Services.Search;
using FluentAssertions;
using ManyFaces.Search.V1;
using Xunit;

namespace BeDemo.Api.Tests.Search;

public sealed class SearchRouteParamsNormalizerEdgeTests
{
	[Theory]
	[InlineData(SearchDocumentTypes.Album, "42", "id", "42", "albumId")]
	[InlineData(SearchDocumentTypes.Page, "7", "id", "7", "pageId")]
	[InlineData(SearchDocumentTypes.Blog, "3", "id", "3", "blogId")]
	public void GSH1_T_RPN01_LegacyIdKey_MappedToTypedKey(
		string type,
		string entityId,
		string legacyKey,
		string legacyValue,
		string expectedKey)
	{
		var route = new RouteParams
		{
			Type = type,
			Ids = { [legacyKey] = legacyValue },
		};

		var normalized = SearchRouteParamsNormalizer.Normalize(type, entityId, route);

		normalized.Should().ContainKey(expectedKey);
		normalized[expectedKey].Should().Be(legacyValue);
		normalized.Should().NotContainKey("id");
	}

	[Fact]
	public void GSH1_T_RPN02_EntityIdFallback_WhenRouteMissingTypedKey()
	{
		var normalized = SearchRouteParamsNormalizer.Normalize(SearchDocumentTypes.Reel, "99", null);

		normalized.Should().ContainKey("reelId");
		normalized["reelId"].Should().Be("99");
	}

	[Fact]
	public void GSH1_T_RPN03_PreservesUserAndFaceKeys()
	{
		var route = new RouteParams
		{
			Type = SearchDocumentTypes.User,
			Ids = { ["userId"] = "u1" },
		};

		var normalized = SearchRouteParamsNormalizer.Normalize(SearchDocumentTypes.User, "u1", route);

		normalized["userId"].Should().Be("u1");
	}
}
