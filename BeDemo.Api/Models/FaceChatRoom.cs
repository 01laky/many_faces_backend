using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

/// <summary>Chat room scoped to a face. User-created or system (admin-only delete).</summary>
public class FaceChatRoom
{
	[Key]
	public int Id { get; set; }

	[Required]
	public int FaceId { get; set; }

	[ForeignKey(nameof(FaceId))]
	public Face Face { get; set; } = null!;

	[Required]
	[StringLength(200)]
	public string Title { get; set; } = string.Empty;

	[StringLength(2000)]
	public string? Description { get; set; }

	/// <summary>When false, users must be approved to join.</summary>
	public bool IsPublic { get; set; } = true;

	/// <summary>Admin-created; only global admin can delete.</summary>
	public bool IsSystemManaged { get; set; }

	/// <summary>Null for system rooms.</summary>
	public string? CreatorUserId { get; set; }

	[ForeignKey(nameof(CreatorUserId))]
	public ApplicationUser? Creator { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? UpdatedAt { get; set; }

	/// <summary>Last chat message time; drives idle expiry.</summary>
	public DateTime? LastMessageAt { get; set; }

	public ICollection<FaceChatRoomMember> Members { get; set; } = new List<FaceChatRoomMember>();
	public ICollection<FaceChatRoomMessage> Messages { get; set; } = new List<FaceChatRoomMessage>();
	public ICollection<FaceChatRoomJoinRequest> JoinRequests { get; set; } = new List<FaceChatRoomJoinRequest>();
}
