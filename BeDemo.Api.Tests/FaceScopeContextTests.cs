using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace BeDemo.Api.Tests;

public sealed class FaceScopeContextTests
{
	[Fact]
	public void Ctor_ShouldBeUnavailable_WhenHttpContextMissing()
	{
		var accessor = new HttpContextAccessor { HttpContext = null };
		var ctx = new FaceScopeContext(accessor);
		ctx.IsAvailable.Should().BeFalse();
		ctx.FaceId.Should().Be(0);
		ctx.ResolveDataFaceId(null).Should().Be(0);
	}

	[Fact]
	public void Ctor_ShouldReadFaceItems_FromHttpContext()
	{
		var http = new DefaultHttpContext();
		http.Items[FaceScopeConstants.RequestFaceIdItemKey] = 42;
		http.Items[FaceScopeConstants.RequestFaceIndexItemKey] = "acme";
		http.Items[FaceScopeConstants.RequestFaceIsPublicItemKey] = true;
		http.Items[FaceScopeConstants.RequestFaceIsAdminScopeItemKey] = false;

		var ctx = new FaceScopeContext(new HttpContextAccessor { HttpContext = http });
		ctx.IsAvailable.Should().BeTrue();
		ctx.FaceId.Should().Be(42);
		ctx.FaceIndex.Should().Be("acme");
		ctx.IsPublicFace.Should().BeTrue();
		ctx.IsAdminFaceScope.Should().BeFalse();
		ctx.ResolveDataFaceId(null).Should().Be(42);
	}

	[Fact]
	public void ResolveDataFaceId_ShouldHonorQueryFaceId_OnAdminScope()
	{
		var http = new DefaultHttpContext();
		http.Items[FaceScopeConstants.RequestFaceIdItemKey] = 1;
		http.Items[FaceScopeConstants.RequestFaceIsAdminScopeItemKey] = true;

		var ctx = new FaceScopeContext(new HttpContextAccessor { HttpContext = http });
		ctx.ResolveDataFaceId(99).Should().Be(99);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(null)]
	public void ResolveDataFaceId_ShouldIgnoreInvalidQueryFaceId_OnAdminScope(int? queryFaceId)
	{
		var http = new DefaultHttpContext();
		http.Items[FaceScopeConstants.RequestFaceIdItemKey] = 7;
		http.Items[FaceScopeConstants.RequestFaceIsAdminScopeItemKey] = true;

		var ctx = new FaceScopeContext(new HttpContextAccessor { HttpContext = http });
		ctx.ResolveDataFaceId(queryFaceId).Should().Be(7);
	}

	[Fact]
	public void ResolveDataFaceId_ShouldIgnoreQueryFaceId_OnTenantScope()
	{
		var http = new DefaultHttpContext();
		http.Items[FaceScopeConstants.RequestFaceIdItemKey] = 3;
		http.Items[FaceScopeConstants.RequestFaceIsAdminScopeItemKey] = false;

		var ctx = new FaceScopeContext(new HttpContextAccessor { HttpContext = http });
		ctx.ResolveDataFaceId(99).Should().Be(3);
	}
}
