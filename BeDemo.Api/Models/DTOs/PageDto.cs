using System.ComponentModel.DataAnnotations;
using BeDemo.Api.Models;

namespace BeDemo.Api.Models.DTOs;

/// <summary>Page route translation entry returned by translation list and update endpoints.</summary>
public sealed class PageRouteTranslationDto
{
	public int Id { get; init; }
	public int PageId { get; init; }
	public string LanguageCode { get; init; } = string.Empty;
	public string TranslatedRoute { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}

/// <summary>Page response DTO returned by list/detail/create/update endpoints.</summary>
public class PageDto
{
	public int Id { get; set; }
	public int FaceId { get; set; }
	public int PageTypeId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Path { get; set; } = string.Empty;
	public int Index { get; set; }
	public string? GridSchema { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }

	/// <summary>Maps a <see cref="Page"/> entity to this DTO.</summary>
	public static PageDto From(Page page) => new()
	{
		Id = page.Id,
		FaceId = page.FaceId,
		PageTypeId = page.PageTypeId,
		Name = page.Name,
		Description = page.Description,
		Path = page.Path,
		Index = page.Index,
		GridSchema = page.GridSchema,
		CreatedAt = page.CreatedAt,
		UpdatedAt = page.UpdatedAt,
	};
}

/// <summary>
/// Create Page DTO - used when creating a new page
/// </summary>
public class CreatePageDto
{
	[Required(ErrorMessage = "FaceId is required")]
	public int FaceId { get; set; }

	[Required(ErrorMessage = "PageTypeId is required")]
	public int PageTypeId { get; set; }

	[Required(ErrorMessage = "Name is required")]
	[StringLength(200, ErrorMessage = "Name must be at most 200 characters")]
	public string Name { get; set; } = string.Empty;

	[StringLength(1000, ErrorMessage = "Description must be at most 1000 characters")]
	public string? Description { get; set; }

	[Required(ErrorMessage = "Path is required")]
	[StringLength(500, ErrorMessage = "Path must be at most 500 characters")]
	public string Path { get; set; } = string.Empty;

	public int Index { get; set; } = 0;
}

/// <summary>
/// Update Page DTO - used when updating an existing page
/// </summary>
public class UpdatePageDto
{
	public int? FaceId { get; set; }

	public int? PageTypeId { get; set; }

	[StringLength(200, ErrorMessage = "Name must be at most 200 characters")]
	public string? Name { get; set; }

	[StringLength(1000, ErrorMessage = "Description must be at most 1000 characters")]
	public string? Description { get; set; }

	[StringLength(500, ErrorMessage = "Path must be at most 500 characters")]
	public string? Path { get; set; }

	public int? Index { get; set; }
}
