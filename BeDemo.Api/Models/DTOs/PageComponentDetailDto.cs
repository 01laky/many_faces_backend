namespace BeDemo.Api.Models.DTOs;

/// <summary>Full page-component detail returned by GET /api/pagecomponents/{id}.</summary>
public sealed class PageComponentDetailDto
{
	public int Id { get; init; }
	public int PageId { get; init; }
	public string GridKey { get; init; } = string.Empty;
	public PageComponentTypeRefDto? ComponentType { get; init; }
	public PageComponentDisplayModeRefDto? DisplayMode { get; init; }
	public int X { get; init; }
	public int Y { get; init; }
	public int W { get; init; }
	public int H { get; init; }
	public int MinW { get; init; }
	public int MinH { get; init; }
	public string? Label { get; init; }
	public string? Title { get; init; }
	public string? Icon { get; init; }
	public DateTime CreatedAt { get; init; }
}

/// <summary>ComponentType reference within a PageComponentDetailDto.</summary>
public sealed class PageComponentTypeRefDto
{
	public int Id { get; init; }
	public string Index { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
}

/// <summary>DisplayMode reference within a PageComponentDetailDto.</summary>
public sealed class PageComponentDisplayModeRefDto
{
	public int Id { get; init; }
	public string Index { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
}

/// <summary>ComponentType detail returned by GET /api/componenttypes/{id}.</summary>
public sealed class ComponentTypeDetailDto
{
	public int Id { get; init; }
	public string Index { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}

/// <summary>DisplayMode detail returned by GET /api/displaymodes/{id}.</summary>
public sealed class DisplayModeDetailDto
{
	public int Id { get; init; }
	public string Index { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}

/// <summary>PageType create/detail response.</summary>
public sealed class PageTypeDetailDto
{
	public int Id { get; init; }
	public string Index { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}
