using BeDemo.Api.Models;
using BeDemo.Api.Scripts;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class AlbumDemoMediaSeedHelperTests
{
	[Theory]
	[InlineData(1)]
	[InlineData(42)]
	[InlineData(999)]
	public void GetImageCountForAlbum_is_between_5_and_20(int albumId)
	{
		var count = AlbumDemoMediaSeedHelper.GetImageCountForAlbum(albumId);
		Assert.InRange(count, 5, 20);
	}

	[Fact]
	public void BuildMediaRows_has_target_images_plus_two_videos()
	{
		const int albumId = 123;
		var rows = AlbumDemoMediaSeedHelper.BuildMediaRows(albumId);

		var images = rows.Where(r => r.MediaType == MediaTypeEnum.Image).ToList();
		var videos = rows.Where(r => r.MediaType == MediaTypeEnum.Video).ToList();

		Assert.Equal(AlbumDemoMediaSeedHelper.GetImageCountForAlbum(albumId), images.Count);
		Assert.Equal(2, videos.Count);
		Assert.All(images, i => Assert.Contains("picsum.photos", i.ImageUrl));
		Assert.All(videos, v =>
		{
			Assert.False(string.IsNullOrWhiteSpace(v.VideoUrl));
			Assert.Contains("picsum.photos", v.ImageUrl);
		});

		var aspects = images
			.Select(i =>
			{
				var parts = i.ImageUrl.Split('/');
				return (W: int.Parse(parts[^2]), H: int.Parse(parts[^1]));
			})
			.Distinct()
			.ToList();
		Assert.True(aspects.Count >= 3, "Expected multiple distinct image aspect ratios");

		var posterAspects = videos
			.Select(v =>
			{
				var parts = v.ImageUrl.Split('/');
				return (W: int.Parse(parts[^2]), H: int.Parse(parts[^1]));
			})
			.Distinct()
			.ToList();
		Assert.Equal(2, posterAspects.Count);
		Assert.NotEqual(posterAspects[0], posterAspects[1]);
	}

	[Fact]
	public void BuildMediaRows_is_deterministic_for_same_album()
	{
		var a = AlbumDemoMediaSeedHelper.BuildMediaRows(77);
		var b = AlbumDemoMediaSeedHelper.BuildMediaRows(77);
		Assert.Equal(a.Count, b.Count);
		Assert.Equal(
			a.Select(x => x.ImageUrl).ToList(),
			b.Select(x => x.ImageUrl).ToList());
	}
}
