using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;

namespace BeDemo.Api.Services;

public sealed class FaceWallTicketLifecycleService : IFaceWallTicketLifecycleService
{
	public const string JobTypeWallTicketDelete = "wall.ticket-delete";

	/// <summary>Denied tickets remain visible until this period elapses, then the worker hard-deletes them.</summary>
	public static readonly TimeSpan DeniedRetention = TimeSpan.FromDays(2);

	private readonly ApplicationDbContext _context;
	private readonly IRedisJobQueue _jobQueue;
	private readonly ILogger<FaceWallTicketLifecycleService> _logger;

	public FaceWallTicketLifecycleService(
		ApplicationDbContext context,
		IRedisJobQueue jobQueue,
		ILogger<FaceWallTicketLifecycleService> logger)
	{
		_context = context;
		_jobQueue = jobQueue;
		_logger = logger;
	}

	public async Task DeleteTicketHardAsync(int ticketId, CancellationToken cancellationToken = default)
	{
		var ticket = await _context.FaceWallTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
		if (ticket == null)
			return;

		_context.FaceWallTickets.Remove(ticket);
		await _context.SaveChangesAsync(cancellationToken);
		_logger.LogInformation("Hard-deleted wall ticket {TicketId}", ticketId);
	}

	public async Task ScheduleDeniedTicketDeletionAsync(int ticketId, CancellationToken cancellationToken = default)
	{
		var payload = JsonSerializer.Serialize(new { wallTicketId = ticketId });
		var runAt = DateTime.UtcNow.Add(DeniedRetention);
		await _jobQueue.ScheduleAsync(JobTypeWallTicketDelete, payload, runAt, cancellationToken);
		_logger.LogInformation("Scheduled wall ticket delete job for ticket {TicketId} at {RunAtUtc}", ticketId, runAt);
	}
}
