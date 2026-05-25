using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Scripts;

/// <summary>
/// Builds remote demo URLs (picsum + public sample MP4s) with varied aspect ratios for seeded albums.
/// </summary>
public static class AlbumDemoMediaSeedHelper
{
	public const string DemoAlbumDescription = "Seeded album for grid demo.";

	/// <summary>Width/height pairs covering landscape, portrait, and square.</summary>
	private static readonly (int W, int H)[] ImageAspectPresets =
	[
		(1920, 1080),
		(1600, 900),
		(1280, 720),
		(1440, 1080),
		(1200, 800),
		(1000, 1500),
		(1080, 1920),
		(900, 1600),
		(720, 1280),
		(800, 1200),
		(1200, 1200),
		(1080, 1080),
		(640, 480),
		(480, 640),
		(1500, 500),
		(500, 1500),
	];

	private sealed record DemoVideoClip(string VideoUrl, string Title, (int W, int H) PosterSize);

	private static readonly DemoVideoClip[] VideoClips =
	[
		new(
			"https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4",
			"Sample clip — flower",
			(1920, 1080)),
		new(
			"https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4",
			"Sample clip — blazes",
			(1080, 1920)),
		new(
			"https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerEscapes.mp4",
			"Sample clip — escapes",
			(1280, 720)),
		new(
			"https://test-videos.co.uk/vids/bigbuckbunny/mp4/h264/360/Big_Buck_Bunny_360_10s_1MB.mp4",
			"Sample clip — bunny",
			(720, 1280)),
	];

	/// <summary>Deterministic 5–20 images per album.</summary>
	public static int GetImageCountForAlbum(int albumId) => 5 + (Math.Abs(albumId) % 16);

	public static IReadOnlyList<AlbumMedia> BuildMediaRows(int albumId, DateTime? createdAt = null)
	{
		var created = createdAt ?? DateTime.UtcNow;
		var rng = new Random(HashCode.Combine(albumId, 0xA1B2C3D4));
		var imageCount = GetImageCountForAlbum(albumId);
		var rows = new List<AlbumMedia>(imageCount + 2);
		var sortOrder = 0;

		for (var i = 0; i < imageCount; i++)
		{
			var (w, h) = ImageAspectPresets[rng.Next(ImageAspectPresets.Length)];
			rows.Add(new AlbumMedia
			{
				AlbumId = albumId,
				MediaType = MediaTypeEnum.Image,
				ImageUrl = PicsumUrl(albumId, "img", i, w, h),
				SortOrder = sortOrder++,
				Title = $"Photo {i + 1} ({w}×{h})",
				CreatedAt = created,
			});
		}

		var videoIndices = PickTwoDistinctIndices(rng, VideoClips.Length);
		foreach (var vi in videoIndices)
		{
			var clip = VideoClips[vi];
			var (pw, ph) = clip.PosterSize;
			rows.Add(new AlbumMedia
			{
				AlbumId = albumId,
				MediaType = MediaTypeEnum.Video,
				ImageUrl = PicsumUrl(albumId, $"vid{vi}-poster", 0, pw, ph),
				VideoUrl = clip.VideoUrl,
				SortOrder = sortOrder++,
				Title = clip.Title,
				CreatedAt = created,
			});
		}

		return rows;
	}

	private static string PicsumUrl(int albumId, string kind, int index, int width, int height) =>
		$"https://picsum.photos/seed/mf-album-{albumId}-{kind}-{index}/{width}/{height}";

	private static int[] PickTwoDistinctIndices(Random rng, int length)
	{
		var first = rng.Next(length);
		var second = rng.Next(length - 1);
		if (second >= first)
			second++;
		return [first, second];
	}

	/// <summary>
	/// Idempotent: adds or replaces media when counts differ from the demo target (5–20 images + 2 videos).
	/// </summary>
	/// <returns>True when rows were replaced.</returns>
	public static async Task<bool> EnsureDemoMediaForAlbumAsync(ApplicationDbContext context, int albumId)
	{
		var targetImages = GetImageCountForAlbum(albumId);
		var counts = await context.AlbumMedia
			.Where(m => m.AlbumId == albumId)
			.GroupBy(m => m.MediaType)
			.Select(g => new { g.Key, Count = g.Count() })
			.ToListAsync();

		var images = counts.FirstOrDefault(c => c.Key == MediaTypeEnum.Image)?.Count ?? 0;
		var videos = counts.FirstOrDefault(c => c.Key == MediaTypeEnum.Video)?.Count ?? 0;

		if (images == targetImages && videos == 2)
			return false;

		var existing = await context.AlbumMedia.Where(m => m.AlbumId == albumId).ToListAsync();
		context.AlbumMedia.RemoveRange(existing);
		context.AlbumMedia.AddRange(BuildMediaRows(albumId));
		return true;
	}
}
