using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

public class FaceVideoLoungeMember
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

	public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
