namespace BeDemo.Api.Models.DTOs;

/// <summary>Multipart body for POST stories/{id}/images.</summary>
public class StoryImageUploadForm
{
	public IFormFile File { get; set; } = null!;
	public string? Description { get; set; }
	public int SortOrder { get; set; }
}
