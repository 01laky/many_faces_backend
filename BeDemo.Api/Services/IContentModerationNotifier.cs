namespace BeDemo.Api.Services;

/// <summary>
/// Persists in-app <see cref="BeDemo.Api.Models.Notification"/> rows for moderation lifecycle events.
/// Creators receive safe, non-technical copy; super-admins receive operational summaries (no internal AI trace leakage in titles/messages passed here—callers must still avoid secrets).
/// </summary>
public interface IContentModerationNotifier
{
    /// <summary>Queues a notification for the content owner (must be followed by <c>SaveChanges</c> on the same unit of work).</summary>
    void NotifyCreator(string creatorId, string title, string message, string type = "content_moderation");

    /// <summary>Loads all global super-admin user ids and queues one notification per admin.</summary>
    Task NotifySuperAdminsAsync(string title, string message, string type = "moderation_ops", CancellationToken cancellationToken = default);
}
