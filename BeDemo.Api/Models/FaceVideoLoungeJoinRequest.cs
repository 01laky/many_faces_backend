using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

public enum FaceVideoLoungeJoinRequestStatus
{
	Pending = 0,
	Approved = 1,
	Denied = 2,
}

public class FaceVideoLoungeJoinRequest
{
	[Key]
	public int Id { get; set; }

	[Required]
	public int FaceVideoLoungeId { get; set; }

	[ForeignKey(nameof(FaceVideoLoungeId))]
	public FaceVideoLounge Lounge { get; set; } = null!;

	[Required]
	[MaxLength(450)]
	public string UserId { get; set; } = string.Empty;

	[ForeignKey(nameof(UserId))]
	public ApplicationUser User { get; set; } = null!;

	public FaceVideoLoungeJoinRequestStatus Status { get; set; } = FaceVideoLoungeJoinRequestStatus.Pending;

	public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}
