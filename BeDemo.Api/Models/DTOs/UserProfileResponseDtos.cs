namespace BeDemo.Api.Models.DTOs;

/// <summary>GET /api/profile/me response.</summary>
public sealed class UserProfileResponseDto
{
	public string? FirstName { get; init; }
	public string? LastName { get; init; }
	public string? Email { get; init; }
	public bool EnableAnimatedGradient { get; init; }
	public string? PreferredUiLanguage { get; init; }
	public int? LastSelectedFaceId { get; init; }
	public string? GlobalAvatarUrl { get; init; }
	public string? FaceAvatarUrl { get; init; }
}

/// <summary>Face profile detail returned by GET /api/faces/{faceId}/profiles/{userId}.</summary>
public sealed class FaceProfileDetailDto
{
	public string? UserId { get; init; }
	public int UserFaceProfileId { get; init; }
	public string? DisplayName { get; init; }
	public string? Nickname { get; init; }
	public int? Age { get; init; }
	public string? Rod { get; init; }
	public string? AvatarUrl { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime UpdatedAt { get; init; }
	public bool FaceAllowsRecensions { get; init; }
	public string FaceVisibility { get; init; } = string.Empty;
	public string FaceRoleName { get; init; } = string.Empty;
	public bool IsActive { get; init; }
	public bool Visited { get; init; }
	public int CommentsCount { get; init; }
	public int LikesCount { get; init; }
	public int ReviewsCount { get; init; }
	public bool IsFaceBanned { get; init; }
	public string? Email { get; init; }
	public bool LikedByMe { get; init; }
}

/// <summary>Operator face-profile list item — returned in the paginated operator profile roster.</summary>
public sealed class OperatorFaceProfileListItemDto
{
	public string UserId { get; init; } = string.Empty;
	public string? DisplayName { get; init; }
	public string? AvatarUrl { get; init; }
	public int CommentsCount { get; init; }
	public int LikesCount { get; init; }
	public int ReviewsCount { get; init; }
	public bool IsFaceBanned { get; init; }
}

/// <summary>User who liked a profile — returned in GET /api/faces/{faceId}/profiles/{userId}/likes.</summary>
public sealed class ProfileLikerDto
{
	public string UserId { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}

/// <summary>Profile comment returned by POST /api/faces/{faceId}/profiles/{userId}/comments.</summary>
public sealed class ProfileCommentResultDto
{
	public int Id { get; init; }
	public string? UserId { get; init; }
	public string? Body { get; init; }
	public DateTime CreatedAt { get; init; }
}

/// <summary>Profile review create/get response.</summary>
public sealed class ReviewResultDto
{
	public int Id { get; init; }
	public string? AuthorUserId { get; init; }
	public string? Title { get; init; }
	public string? Text { get; init; }
	public int Stars { get; init; }
	public DateTime CreatedAt { get; init; }
}
