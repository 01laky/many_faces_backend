namespace BeDemo.Api.Services;

public interface IFaceWallTicketLifecycleService
{
	/// <summary>Hard-delete ticket and all comments/likes.</summary>
	Task DeleteTicketHardAsync(int ticketId, CancellationToken cancellationToken = default);

	/// <summary>After deny: schedule Redis job to delete ticket after retention period.</summary>
	Task ScheduleDeniedTicketDeletionAsync(int ticketId, CancellationToken cancellationToken = default);
}
