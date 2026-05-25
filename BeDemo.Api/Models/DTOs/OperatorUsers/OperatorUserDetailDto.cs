namespace BeDemo.Api.Models.DTOs.OperatorUsers;

public sealed class OperatorUserDetailDto
{
	public string Id { get; set; } = null!;
	public string? Email { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public DateTime CreatedAt { get; set; }
	public OperatorUserGlobalRoleDto GlobalRole { get; set; } = null!;
	public OperatorUserBadgesDto Badges { get; set; } = null!;
	public List<OperatorUserFaceRowDto> Faces { get; set; } = new();
}

public sealed class OperatorUserGlobalRoleDto
{
	public int UserRoleId { get; set; }
	public string Name { get; set; } = null!;
}

public sealed class OperatorUserBadgesDto
{
	public bool IsGloballyBanned { get; set; }
	public int ActiveFaceBanCount { get; set; }
	public bool EmailConfirmed { get; set; }
	public int AccessTokenVersion { get; set; }
}

public sealed class OperatorUserFaceRowDto
{
	public int FaceId { get; set; }
	public string FaceIndex { get; set; } = null!;
	public string FaceTitle { get; set; } = null!;
	public int UserRoleId { get; set; }
	public string RoleName { get; set; } = null!;
	public bool IsActiveParticipant { get; set; }
	public bool IsFaceBanned { get; set; }
}
