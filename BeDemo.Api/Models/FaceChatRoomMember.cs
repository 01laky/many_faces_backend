using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

public class FaceChatRoomMember
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

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
