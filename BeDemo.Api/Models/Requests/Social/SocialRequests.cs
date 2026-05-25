namespace BeDemo.Api.Models.Requests.Social;

public class SendFriendRequestDto
{
	public string? ReceiverId { get; set; }
}

public class BlockUserDto
{
	public string? BlockedId { get; set; }
}

public class FollowUserDto
{
	public string? FollowedId { get; set; }
}

