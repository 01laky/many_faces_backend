using BeDemo.Api.Models;

namespace BeDemo.Api.Models.DTOs;

/// <summary>Face summary returned by list/detail/create/update endpoints.</summary>
public sealed class FaceDto
{
	public int Id { get; init; }
	public string Index { get; init; } = string.Empty;
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? GradientSettings { get; init; }
	public bool IsPublic { get; init; }
	public string Visibility { get; init; } = string.Empty;
	public bool AllowRecensions { get; init; }
	public bool ChatRoomsCreate { get; init; }
	public bool VideoLoungesCreate { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }

	/// <summary>Maps a <see cref="Face"/> entity to this DTO.</summary>
	public static FaceDto From(Face face) => new()
	{
		Id = face.Id,
		Index = face.Index,
		Title = face.Title,
		Description = face.Description,
		GradientSettings = face.GradientSettings,
		IsPublic = face.IsPublic,
		Visibility = face.Visibility.ToString(),
		AllowRecensions = face.AllowRecensions,
		ChatRoomsCreate = face.ChatRoomsCreate,
		VideoLoungesCreate = face.VideoLoungesCreate,
		CreatedAt = face.CreatedAt,
		UpdatedAt = face.UpdatedAt,
	};
}
