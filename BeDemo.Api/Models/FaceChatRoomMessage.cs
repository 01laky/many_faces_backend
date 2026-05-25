using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

public class FaceChatRoomMessage
{
	[Key]
	public int Id { get; set; }

	[Required]
	public int FaceChatRoomId { get; set; }

	[ForeignKey(nameof(FaceChatRoomId))]
	public FaceChatRoom Room { get; set; } = null!;

	[Required]
	[MaxLength(450)]
	public string SenderUserId { get; set; } = string.Empty;

	[ForeignKey(nameof(SenderUserId))]
	public ApplicationUser Sender { get; set; } = null!;

	[Required]
	[StringLength(8000)]
	public string Content { get; set; } = string.Empty;

	public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
