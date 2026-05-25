namespace BeDemo.Api.Models;

public class FriendRequest
{
	public int Id { get; set; }
	public string SenderId { get; set; } = null!;
	public string ReceiverId { get; set; } = null!;
	public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? RespondedAt { get; set; }

	public ApplicationUser Sender { get; set; } = null!;
	public ApplicationUser Receiver { get; set; } = null!;
}

public enum FriendRequestStatus
{
	Pending = 0,
	Accepted = 1,
	Rejected = 2
}
