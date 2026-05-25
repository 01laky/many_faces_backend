namespace BeDemo.Api.Models.Requests.Social;

public sealed class MessageHistoryQuery
{
	public int Limit { get; set; } = 50;
}

public sealed class NotificationsListQuery
{
	public int Limit { get; set; } = 50;
}
