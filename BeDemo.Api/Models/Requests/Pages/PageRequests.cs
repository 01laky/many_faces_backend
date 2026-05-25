namespace BeDemo.Api.Models.Requests.Pages;

/// <summary>
/// Model for creating a new page
/// </summary>
public class CreatePageModel
{
	public int FaceId { get; set; }

	public int PageTypeId { get; set; }

	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public string Path { get; set; } = string.Empty;

	public int Index { get; set; } = 0;

	public string? GridSchema { get; set; }
}

/// <summary>
/// Model for updating a page
/// </summary>
public class UpdatePageModel
{
	public int? FaceId { get; set; }

	public int? PageTypeId { get; set; }

	public string? Name { get; set; }

	public string? Description { get; set; }

	public string? Path { get; set; }

	public int? Index { get; set; }

	public string? GridSchema { get; set; }
}

/// <summary>
/// Model for page route translation
/// </summary>
public class PageRouteTranslationModel
{
	public string LanguageCode { get; set; } = string.Empty;

	public string TranslatedRoute { get; set; } = string.Empty;
}

/// <summary>
/// Model for creating a new page type
/// </summary>
public class CreatePageTypeModel
{
	public string Index { get; set; } = string.Empty;
}

/// <summary>
/// Model for updating a page type
/// </summary>
public class UpdatePageTypeModel
{
	public string? Index { get; set; }
}

public class CreatePageComponentDto
{
	public int PageId { get; set; }
	public int ComponentTypeId { get; set; }
	public int DisplayModeId { get; set; }
	public string? GridKey { get; set; }
	public int X { get; set; }
	public int Y { get; set; }
	public int W { get; set; }
	public int H { get; set; }
	public int MinW { get; set; }
	public int MinH { get; set; }
	public string? Label { get; set; }
	public string? Title { get; set; }
	public string? Icon { get; set; }
}

public class UpdatePageComponentDto
{
	public int? ComponentTypeId { get; set; }
	public int? DisplayModeId { get; set; }
	public int? X { get; set; }
	public int? Y { get; set; }
	public int? W { get; set; }
	public int? H { get; set; }
	public int? MinW { get; set; }
	public int? MinH { get; set; }
	public string? Label { get; set; }
	public string? Title { get; set; }
	public string? Icon { get; set; }
}

