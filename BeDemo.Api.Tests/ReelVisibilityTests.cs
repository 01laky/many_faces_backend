using FluentAssertions;
using Xunit;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Tests;

public class ReelVisibilityTests
{
	[Fact]
	public void IsVisibleForFace_ShouldReturnTrue_WhenNoFaceRows()
	{
		var reel = new Reel { ReelFaces = new List<ReelFace>() };
		ReelVisibility.IsVisibleForFace(reel, null).Should().BeTrue();
		ReelVisibility.IsVisibleForFace(reel, 5).Should().BeTrue();
	}

	[Fact]
	public void IsVisibleForFace_ShouldMatchListedFace()
	{
		var reel = new Reel
		{
			ReelFaces = new List<ReelFace> { new() { FaceId = 2 } }
		};
		ReelVisibility.IsVisibleForFace(reel, null).Should().BeFalse();
		ReelVisibility.IsVisibleForFace(reel, 2).Should().BeTrue();
		ReelVisibility.IsVisibleForFace(reel, 3).Should().BeFalse();
	}
}
