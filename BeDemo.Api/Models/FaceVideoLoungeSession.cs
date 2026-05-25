using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

/// <summary>One live session per lounge; <see cref="EndedAt"/> null means active.</summary>
public class FaceVideoLoungeSession
{
	[Key]
	public int Id { get; set; }

	[Required]
	public int FaceVideoLoungeId { get; set; }

	[ForeignKey(nameof(FaceVideoLoungeId))]
	public FaceVideoLounge Lounge { get; set; } = null!;

	[Required]
	[MaxLength(450)]
	public string StartedByUserId { get; set; } = string.Empty;

	public DateTime StartedAt { get; set; } = DateTime.UtcNow;

	public DateTime? EndedAt { get; set; }

	/// <summary>Last activity for idle session cleanup.</summary>
	public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

	public ICollection<FaceVideoLoungeSessionParticipant> Participants { get; set; } =
		new List<FaceVideoLoungeSessionParticipant>();
}
