namespace BeDemo.Api.Models.DTOs.Admin;

public sealed class AdminMeProfileDto
{
	public string Id { get; set; } = null!;
	public string? Email { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public DateTime CreatedAt { get; set; }
	public AdminMeGlobalRoleDto GlobalRole { get; set; } = null!;
	public bool EmailConfirmed { get; set; }
	public string? GlobalAvatarUrl { get; set; }
	public IReadOnlyList<AdminMeFaceRowDto> Faces { get; set; } = Array.Empty<AdminMeFaceRowDto>();
}

public sealed class AdminMeGlobalRoleDto
{
	public int UserRoleId { get; set; }
	public string Name { get; set; } = null!;
}

public sealed class AdminMeFaceRowDto
{
	public int FaceId { get; set; }
	public string FaceIndex { get; set; } = null!;
	public string FaceTitle { get; set; } = null!;
	public int? UserRoleId { get; set; }
	public string? RoleName { get; set; }
	public bool HasMembership { get; set; }
	public bool IsActiveParticipant { get; set; }
}
