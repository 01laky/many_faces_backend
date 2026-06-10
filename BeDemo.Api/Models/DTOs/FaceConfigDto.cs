namespace BeDemo.Api.Models.DTOs;

/// <summary>Typed face-config entry returned by <c>/api/faces-config</c>.</summary>
public sealed class FaceConfigDto
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
	public int? MyFaceRoleId { get; init; }
	public string? MyFaceRoleName { get; init; }
	public bool? MyVisited { get; init; }
	public bool? MyFaceRoleIntroCompleted { get; init; }
	public List<FaceConfigPageDto> Pages { get; init; } = [];
}

/// <summary>Page entry within a <see cref="FaceConfigDto"/>.</summary>
public sealed class FaceConfigPageDto
{
	public int Id { get; init; }
	public int Index { get; init; }
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string Path { get; init; } = string.Empty;
	public string? GridSchema { get; init; }
	public FaceConfigPageTypeDto? PageType { get; init; }
	public List<FaceConfigRouteTranslationDto> RouteTranslations { get; init; } = [];
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}

/// <summary>Page-type stub within a <see cref="FaceConfigPageDto"/>.</summary>
public sealed class FaceConfigPageTypeDto
{
	public int Id { get; init; }
	public string Index { get; init; } = string.Empty;
}

/// <summary>Route translation entry within a <see cref="FaceConfigPageDto"/>.</summary>
public sealed class FaceConfigRouteTranslationDto
{
	public string LanguageCode { get; init; } = string.Empty;
	public string TranslatedRoute { get; init; } = string.Empty;
}
