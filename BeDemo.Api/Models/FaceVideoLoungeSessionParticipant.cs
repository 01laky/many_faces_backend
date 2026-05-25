using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

public class FaceVideoLoungeSessionParticipant
{
	[Key]
	public int Id { get; set; }

	[Required]
	public int FaceVideoLoungeSessionId { get; set; }

	[ForeignKey(nameof(FaceVideoLoungeSessionId))]
	public FaceVideoLoungeSession Session { get; set; } = null!;

	[Required]
	[MaxLength(450)]
	public string UserId { get; set; } = string.Empty;

	[ForeignKey(nameof(UserId))]
	public ApplicationUser User { get; set; } = null!;

	public VideoLoungeJoinMode JoinMode { get; set; }

	public bool AudioEnabled { get; set; } = true;

	public bool VideoEnabled { get; set; }

	public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

	public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

	/// <summary>When false (AdminStealth), omitted from portal roster and public counts.</summary>
	public bool IsListedInPublicRoster { get; set; } = true;

	public DateTime? LeftAt { get; set; }
}
