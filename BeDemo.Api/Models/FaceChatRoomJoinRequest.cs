using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

public enum FaceChatRoomJoinRequestStatus
{
	Pending = 0,
	Approved = 1,
	Denied = 2,
}

public class FaceChatRoomJoinRequest
{
	[Key]
	public int Id { get; set; }

	[Required]
	public int FaceChatRoomId { get; set; }

	[ForeignKey(nameof(FaceChatRoomId))]
	public FaceChatRoom Room { get; set; } = null!;

	[Required]
	[MaxLength(450)]
	public string UserId { get; set; } = string.Empty;

	[ForeignKey(nameof(UserId))]
	public ApplicationUser User { get; set; } = null!;

	public FaceChatRoomJoinRequestStatus Status { get; set; } = FaceChatRoomJoinRequestStatus.Pending;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? ResolvedAt { get; set; }
}
