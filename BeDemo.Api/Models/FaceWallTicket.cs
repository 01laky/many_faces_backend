using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

public class FaceWallTicket
{
	public int Id { get; set; }

	public int FaceId { get; set; }

	[Required]
	[StringLength(450)]
	public string CreatorUserId { get; set; } = null!;

	[Required]
	[StringLength(200)]
	public string Title { get; set; } = string.Empty;

	[Required]
	public string Description { get; set; } = string.Empty;

	public FaceWallTicketStatus Status { get; set; } = FaceWallTicketStatus.Active;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? UpdatedAt { get; set; }

	[ForeignKey(nameof(FaceId))]
	public Face Face { get; set; } = null!;

	[ForeignKey(nameof(CreatorUserId))]
	public ApplicationUser Creator { get; set; } = null!;

	public ICollection<FaceWallTicketComment> Comments { get; set; } = new List<FaceWallTicketComment>();

	public ICollection<FaceWallTicketLike> Likes { get; set; } = new List<FaceWallTicketLike>();
}
