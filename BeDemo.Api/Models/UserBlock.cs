namespace BeDemo.Api.Models;

public class UserBlock
{
	public int Id { get; set; }
	public string BlockerId { get; set; } = null!;
	public string BlockedId { get; set; } = null!;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public ApplicationUser Blocker { get; set; } = null!;
	public ApplicationUser Blocked { get; set; } = null!;
}
