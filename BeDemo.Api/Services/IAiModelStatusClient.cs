namespace BeDemo.Api.Services;

/// <summary>
/// Health probe surface used by Activate AI orchestration.
/// Resolved from the unguarded <see cref="AiGrpcService"/> so enable is not blocked by the availability decorator.
/// </summary>
public interface IAiModelStatusClient
{
	Task<AiModelStatus> GetModelStatusAsync(CancellationToken cancellationToken = default);
}
