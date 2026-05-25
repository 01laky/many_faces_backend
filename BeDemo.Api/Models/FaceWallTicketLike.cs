using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

public class FaceWallTicketLike
{
	public int Id { get; set; }

	public int FaceWallTicketId { get; set; }

	[Required]
	[StringLength(450)]
	public string UserId { get; set; } = null!;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	[ForeignKey(nameof(FaceWallTicketId))]
	public FaceWallTicket Ticket { get; set; } = null!;

	[ForeignKey(nameof(UserId))]
	public ApplicationUser User { get; set; } = null!;
}
