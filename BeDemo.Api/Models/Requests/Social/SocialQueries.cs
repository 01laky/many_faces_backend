namespace BeDemo.Api.Models.Requests.Social;

public sealed class MessageHistoryQuery
{
	public int Limit { get; set; } = 50;

	/// <summary>Cursor: return messages with id strictly less than this value (MO-SR43-BE1).</summary>
	public int? BeforeId { get; set; }
}

public sealed class NotificationsListQuery
{
	public int Limit { get; set; } = 50;
}
