namespace BeDemo.Api.Models;

/// <summary>Platform operator face-scoped ban (distinct from peer <see cref="UserBlock"/>).</summary>
public class UserFaceModeration
{
	public int Id { get; set; }
	public string UserId { get; set; } = null!;
	public int FaceId { get; set; }
	public DateTime BannedAt { get; set; } = DateTime.UtcNow;
	public string BannedByUserId { get; set; } = null!;
	public string Reason { get; set; } = null!;
	public DateTime? LiftedAt { get; set; }

	public ApplicationUser User { get; set; } = null!;
	public Face Face { get; set; } = null!;
	public ApplicationUser BannedByUser { get; set; } = null!;
}
