namespace BeDemo.Api.Models.Requests.Faces;

using BeDemo.Api.Models;

/// <summary>
/// Model for creating a new face
/// </summary>
public class CreateFaceModel
{
	public string Index { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public string? Description { get; set; }

	public string? GradientSettings { get; set; }

	public bool IsPublic { get; set; } = true;

	public FaceVisibility? Visibility { get; set; }

	public bool? AllowRecensions { get; set; }

	/// <summary>When true, users (non-host) may create chat rooms from the app.</summary>
	public bool? ChatRoomsCreate { get; set; }

	/// <summary>When true, users (non-host) may create video lounges from the app.</summary>
	public bool? VideoLoungesCreate { get; set; }
}

/// <summary>
/// Model for updating a face
/// </summary>
public class UpdateFaceModel
{
	public string? Index { get; set; }

	public string? Title { get; set; }

	public string? Description { get; set; }

	public string? GradientSettings { get; set; }

	public bool? IsPublic { get; set; }

	public FaceVisibility? Visibility { get; set; }

	public bool? AllowRecensions { get; set; }

	public bool? ChatRoomsCreate { get; set; }

	public bool? VideoLoungesCreate { get; set; }
}

/// <summary>
/// Model for setting current user's face role (PUT /api/faces/{id}/my-role)
/// </summary>
public class SetMyFaceRoleModel
{
	public int UserRoleId { get; set; }
}

public class FaceProfileCommentDto
{
	public string Body { get; set; } = string.Empty;
}

public class FaceProfileReviewDto
{
	public string Title { get; set; } = string.Empty;

	public string Text { get; set; } = string.Empty;

	public int? Stars { get; set; }
}

public class CreateFaceChatRoomDto
{
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public bool IsPublic { get; set; } = true;
}

public class CreateSystemFaceChatRoomDto
{
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
}

public class UpdateFaceChatRoomDto
{
	public string? Title { get; set; }
	public string? Description { get; set; }
	public bool? IsPublic { get; set; }
}

public class CreateFaceVideoLoungeDto
{
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public bool IsPublic { get; set; } = true;
	public int MaxParticipants { get; set; } = 12;
}

public class CreateSystemFaceVideoLoungeDto
{
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public int MaxParticipants { get; set; } = 12;
}

public class UpdateFaceVideoLoungeDto
{
	public string? Title { get; set; }
	public string? Description { get; set; }
	public bool? IsPublic { get; set; }
	public int? MaxParticipants { get; set; }
}

