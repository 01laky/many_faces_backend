using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

/// <summary>Standalone face-scoped video/voice lounge — not coupled to <see cref="FaceChatRoom"/>.</summary>
public class FaceVideoLounge
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

	public bool IsPublic { get; set; } = true;

	public bool IsSystemManaged { get; set; }

	public string? CreatorUserId { get; set; }

	[ForeignKey(nameof(CreatorUserId))]
	public ApplicationUser? Creator { get; set; }

	/// <summary>Maximum listed participants in a live session (stealth operators excluded from count).</summary>
	public int MaxParticipants { get; set; } = 12;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? UpdatedAt { get; set; }

	public ICollection<FaceVideoLoungeMember> Members { get; set; } = new List<FaceVideoLoungeMember>();
	public ICollection<FaceVideoLoungeJoinRequest> JoinRequests { get; set; } = new List<FaceVideoLoungeJoinRequest>();
	public ICollection<FaceVideoLoungeSession> Sessions { get; set; } = new List<FaceVideoLoungeSession>();
}
