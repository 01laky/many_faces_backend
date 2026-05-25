using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Tests.Utils;

/// <summary>BE-RA25…RA28 — controller tenant gate extension.</summary>
public sealed class ControllerAccessExtensionsTests
{
	private static ClaimsPrincipal TenantUser() =>
		new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test"));

	[Fact]
	public void BE_RA25_GateTenantFace_AllowsCrossFace_WhenOperatorCanManageAllFaces()
	{
		var scope = new Mock<IFaceScopeContext>();
		scope.Setup(s => s.FaceId).Returns(1);
		var access = new Mock<IAccessEvaluator>();
		access.Setup(a => a.CanManageAllFaces(It.IsAny<ClaimsPrincipal>())).Returns(true);

		var result = scope.Object.GateTenantFaceOrNotFound(access.Object, TenantUser(), targetFaceId: 99);

		result.Should().BeNull();
	}

	[Fact]
	public void BE_RA26_GateTenantFace_AllowsSameScopedFace()
	{
		var scope = new Mock<IFaceScopeContext>();
		scope.Setup(s => s.FaceId).Returns(5);
		var access = new Mock<IAccessEvaluator>();
		access.Setup(a => a.CanManageAllFaces(It.IsAny<ClaimsPrincipal>())).Returns(false);

		var result = scope.Object.GateTenantFaceOrNotFound(access.Object, TenantUser(), targetFaceId: 5);

		result.Should().BeNull();
	}

	[Fact]
	public void BE_RA27_GateTenantFace_BlocksCrossFaceWith404ErrorShape()
	{
		var scope = new Mock<IFaceScopeContext>();
		scope.Setup(s => s.FaceId).Returns(1);
		var access = new Mock<IAccessEvaluator>();
		access.Setup(a => a.CanManageAllFaces(It.IsAny<ClaimsPrincipal>())).Returns(false);

		var result = scope.Object.GateTenantFaceOrNotFound(access.Object, TenantUser(), targetFaceId: 2);

		result.Should().BeOfType<NotFoundObjectResult>();
		var notFound = (NotFoundObjectResult)result!;
		notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
		notFound.Value.Should().BeEquivalentTo(new { error = "Face not found" });
	}

	[Fact]
	public void BE_RA28_GateTenantFace_MatchesTenantFaceAccessGateDirectly()
	{
		var scope = new Mock<IFaceScopeContext>();
		scope.Setup(s => s.FaceId).Returns(10);
		var access = new Mock<IAccessEvaluator>();
		access.Setup(a => a.CanManageAllFaces(It.IsAny<ClaimsPrincipal>())).Returns(false);
		var user = TenantUser();

		var viaExtension = scope.Object.GateTenantFaceOrNotFound(access.Object, user, 11);
		var direct = TenantFaceAccessGate.TryBlockTenantCrossFace(scope.Object, callerCanManageAllFaces: false, 11);

		viaExtension.Should().NotBeNull();
		direct.Should().NotBeNull();
		viaExtension.Should().BeOfType<NotFoundObjectResult>();
		direct.Should().BeOfType<NotFoundObjectResult>();
		((NotFoundObjectResult)viaExtension!).Value.Should().BeEquivalentTo(((NotFoundObjectResult)direct!).Value);
	}
}
