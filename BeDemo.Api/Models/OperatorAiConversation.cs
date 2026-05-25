namespace BeDemo.Api.Models;

/// <summary>
/// Shared support thread for platform operators (admin AI chat). Visible to all operators with <c>CanManageAllFaces</c>.
/// </summary>
public class OperatorAiConversation
{
	public int Id { get; set; }
	public string? Title { get; set; }
	public string CreatedByUserId { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }

	public ApplicationUser? CreatedByUser { get; set; }
	public ICollection<OperatorAiMessage> Messages { get; set; } = new List<OperatorAiMessage>();
}
