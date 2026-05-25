using FluentAssertions;
using Xunit;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Tests;

public class FaceRoleSelfServiceRulesTests
{
	[Fact]
	public void IsSelfAssignableFaceRoleName_False_ForNullEmptyWhitespaceUnknownAndAdmin()
	{
		FaceRoleSelfServiceRules.IsSelfAssignableFaceRoleName(null).Should().BeFalse();
		FaceRoleSelfServiceRules.IsSelfAssignableFaceRoleName("").Should().BeFalse();
		FaceRoleSelfServiceRules.IsSelfAssignableFaceRoleName("   ").Should().BeFalse();
		FaceRoleSelfServiceRules.IsSelfAssignableFaceRoleName("FACE_ADMIN").Should().BeFalse();
		FaceRoleSelfServiceRules.IsSelfAssignableFaceRoleName("face_admin").Should().BeFalse();
		FaceRoleSelfServiceRules.IsSelfAssignableFaceRoleName("UNKNOWN").Should().BeFalse();
	}

	[Theory]
	[InlineData("FACE_USER", true)]
	[InlineData("face_user", true)]
	[InlineData("INZERENT", true)]
	[InlineData("inzerent", true)]
	[InlineData("SUBSCRIBER", true)]
	[InlineData("FACE_HOST", true)]
	[InlineData("Face_Host", true)]
	public void IsSelfAssignableFaceRoleName_AllowsWhitelist(string name, bool expected)
	{
		FaceRoleSelfServiceRules.IsSelfAssignableFaceRoleName(name).Should().Be(expected);
	}

	[Fact]
	public void Whitelist_ContainsExactlyFourDistinctCanonicalNames()
	{
		var names = new[]
		{
			UserRole.FaceRoleNames.FaceUser,
			UserRole.FaceRoleNames.Inzerent,
			UserRole.FaceRoleNames.Subscriber,
			UserRole.FaceRoleNames.FaceHost,
		};
		names.Distinct(StringComparer.Ordinal).Should().HaveCount(4);
		foreach (var n in names)
			FaceRoleSelfServiceRules.IsSelfAssignableFaceRoleName(n).Should().BeTrue();
	}
}
