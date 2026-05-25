using FluentAssertions;
using Xunit;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Tests;

public class FaceRoleParticipationTests
{
	[Theory]
	[InlineData("FACE_HOST", true)]
	[InlineData("face_host", false)]
	[InlineData("FACE_USER", false)]
	[InlineData(null, false)]
	[InlineData("", false)]
	public void IsHostFaceRole_ShouldMatch_FaceHostOnly(string? name, bool expected)
	{
		FaceRoleParticipation.IsHostFaceRole(name).Should().Be(expected);
	}

	[Theory]
	[InlineData("FACE_HOST", false)]
	[InlineData("FACE_USER", true)]
	[InlineData("FACE_ADMIN", true)]
	public void IsActiveForFaceRoleName_ShouldBeNonHost(string name, bool expected)
	{
		FaceRoleParticipation.IsActiveForFaceRoleName(name).Should().Be(expected);
	}
}
