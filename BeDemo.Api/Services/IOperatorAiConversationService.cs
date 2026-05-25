using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Models.Requests.OperatorAi;

namespace BeDemo.Api.Services;

public interface IOperatorAiConversationService
{
	Task<IReadOnlyList<OperatorAiConversationListItemDto>> ListConversationsAsync(
		int limit,
		CancellationToken cancellationToken = default);

	Task<OperatorAiConversationListItemDto?> GetConversationAsync(int id, CancellationToken cancellationToken = default);

	Task<OperatorAiConversationListItemDto> CreateConversationAsync(
		string userId,
		CreateOperatorAiConversationRequest request,
		CancellationToken cancellationToken = default);

	Task<OperatorAiConversationListItemDto?> UpdateConversationAsync(
		int id,
		UpdateOperatorAiConversationRequest request,
		CancellationToken cancellationToken = default);

	Task<bool> DeleteConversationAsync(int id, CancellationToken cancellationToken = default);

	Task<OperatorAiMessagesPageDto> GetMessagesPageAsync(
		int conversationId,
		OperatorAiMessagesQuery query,
		CancellationToken cancellationToken = default);

	Task<(OperatorAiMessageDto User, OperatorAiMessageDto Assistant)> AppendExchangeAsync(
		int conversationId,
		string userId,
		string operatorEmail,
		string responseLocale,
		string userContent,
		string assistantContent,
		string? statsMode,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<ChatHistoryEntry>> GetRecentHistoryPairsAsync(
		int conversationId,
		int maxPairs,
		CancellationToken cancellationToken = default);

	Task EnforceConversationRetentionAsync(CancellationToken cancellationToken = default);
}
