using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BeDemo.Api.Tests.Security;

/// <summary>
/// Characterization tests for the central cross-tenant anti-spoof guard <see cref="FaceScopeContext.ResolveDataFaceId"/>
/// (backend-refactor §4.3): a non-admin face scope must ALWAYS resolve to its own face, ignoring any user-supplied
/// <c>queryFaceId</c>; an admin scope may target another face only with a positive id.
/// </summary>
public sealed class FaceScopeContextResolveDataFaceIdTests
{
	private static IFaceScopeContext Scope(int faceId, bool adminScope)
	{
		var ctx = new DefaultHttpContext();
		ctx.Items[FaceScopeConstants.RequestFaceIdItemKey] = faceId;
		ctx.Items[FaceScopeConstants.RequestFaceIsAdminScopeItemKey] = adminScope;
		return new FaceScopeContext(new HttpContextAccessor { HttpContext = ctx });
	}

	[Theory]
	[InlineData(99)]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(null)]
	public void Non_admin_scope_always_returns_own_face_ignoring_query(int? query)
	{
		Scope(faceId: 7, adminScope: false).ResolveDataFaceId(query)
			.Should().Be(7, "a tenant must never read another face's data by spoofing a query param");
	}

	[Fact]
	public void Admin_scope_honours_a_positive_query_face_id()
	{
		Scope(faceId: 1, adminScope: true).ResolveDataFaceId(42).Should().Be(42);
	}

	[Theory]
	[InlineData(null)]
	[InlineData(0)]
	[InlineData(-5)]
	public void Admin_scope_falls_back_to_own_face_for_missing_or_nonpositive_query(int? query)
	{
		Scope(faceId: 3, adminScope: true).ResolveDataFaceId(query).Should().Be(3);
	}

	[Fact]
	public void No_face_scope_in_context_is_unavailable_and_resolves_to_zero()
	{
		var scope = new FaceScopeContext(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
		scope.IsAvailable.Should().BeFalse();
		scope.IsAdminFaceScope.Should().BeFalse();
		scope.ResolveDataFaceId(50).Should().Be(0, "no scope ⇒ no admin override");
	}
}
