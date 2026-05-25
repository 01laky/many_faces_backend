namespace BeDemo.Api.Models;

public class Message
{
	public int Id { get; set; }
	public string SenderId { get; set; } = null!;
	public string ReceiverId { get; set; } = null!;
	public string Content { get; set; } = null!;
	public DateTime SentAt { get; set; } = DateTime.UtcNow;
	public DateTime? ReadAt { get; set; }
	/// <summary>If true, sender is not a friend - this is a message request.</summary>
	public bool IsMessageRequest { get; set; }
	/// <summary>If IsMessageRequest, status of the request.</summary>
	public MessageRequestStatus? MessageRequestStatus { get; set; }

	/// <summary>True for super-admin platform DMs (not peer social chat).</summary>
	public bool IsPlatformDirectMessage { get; set; }

	public ApplicationUser Sender { get; set; } = null!;
	public ApplicationUser Receiver { get; set; } = null!;
}

public enum MessageRequestStatus
{
	Pending = 0,
	Accepted = 1,
	Rejected = 2
}
