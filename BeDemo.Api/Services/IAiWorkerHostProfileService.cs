using BeDemo.Api.Models.DTOs.OperatorAi;

namespace BeDemo.Api.Services;

public interface IAiWorkerHostProfileService
{
	Task RefreshFromWorkerAsync(CancellationToken cancellationToken = default);

	Task<OperatorAiWorkerHostDto> GetOperatorViewAsync(CancellationToken cancellationToken = default);
}
