/*
 * PageDto.cs - Data Transfer Objects (DTOs) for Page operations
 * 
 * This file contains DTOs used for Page API communication.
 * DTOs separate API contracts from domain entities.
 */

using System.ComponentModel.DataAnnotations;

namespace BeDemo.Api.Models.DTOs;

/// <summary>
/// Page DTO - represents page data returned from API
/// </summary>
public class PageDto
{
	public int Id { get; set; }
	public int FaceId { get; set; }
	public int PageTypeId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Path { get; set; } = string.Empty;
	public int Index { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }
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
