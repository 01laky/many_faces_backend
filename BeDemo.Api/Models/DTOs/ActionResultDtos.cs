using System.Text.Json.Nodes;

namespace BeDemo.Api.Models.DTOs;

/// <summary>Simple success/failure flag returned by write operations.</summary>
public sealed class SuccessResultDto
{
	public bool Success { get; init; }
	public static SuccessResultDto True => new() { Success = true };
	public static SuccessResultDto False => new() { Success = false };
}

/// <summary>Like / unlike toggle result.</summary>
public sealed class LikeResultDto
{
	public bool Liked { get; init; }
	public static LikeResultDto Yes => new() { Liked = true };
	public static LikeResultDto No => new() { Liked = false };
}

/// <summary>Follow-status query result.</summary>
public sealed class IsFollowingDto { public bool IsFollowing { get; init; } }

/// <summary>Block-status query result.</summary>
public sealed class IsBlockedDto { public bool IsBlocked { get; init; } }

/// <summary>Direct-join result for a public room/lounge.</summary>
public sealed class JoinResultDto
{
	public bool Joined { get; init; }
	public bool AlreadyMember { get; init; }
}

/// <summary>Join-request submission result.</summary>
public sealed class JoinRequestResultDto
{
	public int RequestId { get; init; }
	public bool Pending { get; init; }
}

/// <summary>Join-request approval result.</summary>
public sealed class ApprovedResultDto { public bool Approved { get; init; } = true; }

/// <summary>Join-request denial result.</summary>
public sealed class DeniedResultDto { public bool Denied { get; init; } = true; }

/// <summary>Session-start result (just the new session id).</summary>
public sealed class SessionIdResultDto { public int SessionId { get; init; } }

/// <summary>Session or story ended.</summary>
public sealed class EndedResultDto { public bool Ended { get; init; } = true; }

/// <summary>Participant left a live session.</summary>
public sealed class LeftResultDto { public bool Left { get; init; } }

/// <summary>Generic "operation succeeded" flag (heartbeat, etc.).</summary>
public sealed class OkResultDto { public bool Ok { get; init; } = true; }

/// <summary>Single participant kicked from live session.</summary>
public sealed class KickedResultDto { public bool Kicked { get; init; } = true; }

/// <summary>All participants kicked; optional session end.</summary>
public sealed class KickedAllResultDto
{
	public bool KickedAll { get; init; } = true;
	public bool EndSession { get; init; }
}

/// <summary>Minimal created-entity response carrying only the new row id.</summary>
public sealed class CreatedEntityDto { public int Id { get; init; } }

/// <summary>Count-only response (unread count, etc.).</summary>
public sealed class CountResultDto { public int Count { get; init; } }

/// <summary>Story / content published flag.</summary>
public sealed class PublishedResultDto { public bool Published { get; init; } = true; }

/// <summary>View recorded / not recorded.</summary>
public sealed class RecordedResultDto { public bool Recorded { get; init; } }

/// <summary>User global ban result.</summary>
public sealed class BanResultDto
{
	public bool Banned { get; init; }
	public bool AlreadyBanned { get; init; }
}

/// <summary>User face-scoped ban result.</summary>
public sealed class FaceBanResultDto
{
	public bool FaceBanned { get; init; }
	public bool AlreadyBanned { get; init; }
}

/// <summary>Assign-role result carrying only the assigned role id.</summary>
public sealed class UserRoleIdResultDto { public int UserRoleId { get; init; } }

/// <summary>Face-role assignment result.</summary>
public sealed class FaceRoleResultDto
{
	public int UserRoleId { get; init; }
	public string UserRoleName { get; init; } = string.Empty;
}

/// <summary>Face visited flag.</summary>
public sealed class VisitedResultDto { public bool Visited { get; init; } = true; }

/// <summary>Exit-face result with the host role details.</summary>
public sealed class ExitFaceResultDto
{
	public string Message { get; init; } = string.Empty;
	public int UserRoleId { get; init; }
	public string UserRoleName { get; init; } = string.Empty;
}

/// <summary>Operator send-message result.</summary>
public sealed class MessageIdResultDto { public int? MessageId { get; init; } }

/// <summary>Avatar upload result.</summary>
public sealed class AvatarUploadResultDto { public string? AvatarUrl { get; init; } }

/// <summary>Grid component settings response.</summary>
public sealed class GridComponentsResultDto { public JsonNode? GridComponents { get; init; } }

/// <summary>Page-component create result.</summary>
public sealed class PageComponentCreatedDto
{
	public int Id { get; init; }
	public string GridKey { get; init; } = string.Empty;
}

/// <summary>Mailer self-test send result.</summary>
public sealed class MailerTestSentDto
{
	public string CorrelationId { get; init; } = string.Empty;
	public string SmtpMessageId { get; init; } = string.Empty;
}

/// <summary>Generic error response body — wraps a single error string.</summary>
public sealed class ErrorResponseDto { public string Error { get; init; } = string.Empty; }

/// <summary>Error response with a machine-readable code alongside the human message.</summary>
public sealed class ErrorCodeResponseDto
{
	public string Error { get; init; } = string.Empty;
	public string ErrorCode { get; init; } = string.Empty;
}

/// <summary>Error response where a short code comes first (face-ban / ban codes).</summary>
public sealed class CodedErrorResponseDto
{
	public string Code { get; init; } = string.Empty;
	public string Error { get; init; } = string.Empty;
}

/// <summary>Simple message result for auth/profile acknowledgements.</summary>
public sealed class MessageResultDto { public string Message { get; init; } = string.Empty; }

/// <summary>Error response that also carries the app/scope identifier (localization bundle errors).</summary>
public sealed class AppScopedErrorDto
{
	public string Error { get; init; } = string.Empty;
	public string App { get; init; } = string.Empty;
}

/// <summary>Error response with an additional human-readable message (invite/registration errors).</summary>
public sealed class ErrorWithMessageDto
{
	public string Error { get; init; } = string.Empty;
	public string Message { get; init; } = string.Empty;
}

/// <summary>Wall-ticket admin status-change result (id + current status).</summary>
public sealed class WallTicketStatusResultDto
{
	public int Id { get; init; }
	public string Status { get; init; } = string.Empty;
}

/// <summary>Face role summary (id + name) used in the face-roles selector list.</summary>
public sealed class FaceRoleSummaryDto
{
	public int Id { get; init; }
	public string Name { get; init; } = string.Empty;
}

/// <summary>Push broadcast test result returned by POST /api/admin/push/test.</summary>
public sealed class AdminPushTestResultDto
{
	public int Sent { get; init; }
	public int Failed { get; init; }
	public int PrunedInvalidTokens { get; init; }
}

/// <summary>Friend summary returned in the friends list.</summary>
public sealed class FriendSummaryDto
{
	public string Id { get; init; } = string.Empty;
	public string? Email { get; init; }
	public string? FirstName { get; init; }
	public string? LastName { get; init; }
}

/// <summary>Chat-room member row in the paginated members list.</summary>
public sealed class ChatRoomMemberDto
{
	public string UserId { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
	public DateTime JoinedAt { get; init; }
}

/// <summary>Pending join-request row in the operator join-requests list.</summary>
public sealed class ChatRoomJoinRequestItemDto
{
	public int RequestId { get; init; }
	public string UserId { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
	public string Status { get; init; } = string.Empty;
}

/// <summary>Pending friend request item returned by GET /api/friendrequests.</summary>
public sealed class FriendRequestItemDto
{
	public int Id { get; init; }
	public string SenderId { get; init; } = string.Empty;
	public string? SenderEmail { get; init; }
	public string SenderName { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}

/// <summary>Follow relationship item returned by GET /api/userfollows/following and /followers.</summary>
public sealed class UserFollowItemDto
{
	public int Id { get; init; }
	public string UserId { get; init; } = string.Empty;
	public string? Email { get; init; }
	public string Name { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}

/// <summary>Block relationship item returned by GET /api/userblocks.</summary>
public sealed class UserBlockItemDto
{
	public int Id { get; init; }
	public string BlockedId { get; init; } = string.Empty;
	public string? BlockedEmail { get; init; }
	public string BlockedName { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}

/// <summary>Like list item returned by GET /api/{content}/{id}/likes endpoints.</summary>
public sealed class ContentLikeItemDto
{
	public int Id { get; init; }
	public string UserId { get; init; } = string.Empty;
	public string UserName { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}

/// <summary>Single notification history item returned by GET /api/notifications.</summary>
public sealed class NotificationHistoryItemDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string Message { get; init; } = string.Empty;
	public string Type { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
}

/// <summary>Result returned by content moderation approve/reject/remove actions.</summary>
public sealed class ModerationDecisionResultDto
{
	public string? ApprovalStatus { get; init; }
	public string? AiReviewStatus { get; init; }
}

/// <summary>Wall ticket list item returned by admin GET /api/admin/faces/{faceId}/wall-tickets.</summary>
public sealed class AdminWallTicketListItemDto
{
	public int Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string DescriptionPreview { get; init; } = string.Empty;
	public string Status { get; init; } = string.Empty;
	public string CreatorId { get; init; } = string.Empty;
	public string CreatorName { get; init; } = string.Empty;
	public int LikesCount { get; init; }
	public int CommentsCount { get; init; }
	public DateTime CreatedAt { get; init; }
}

/// <summary>Profile comment item returned by operator-paginated GET /api/faces/{faceId}/profiles/{userId}/comments.</summary>
public sealed class FaceProfileCommentAdminItemDto
{
	public int Id { get; init; }
	public string UserId { get; init; } = string.Empty;
	public string Body { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
	public string AuthorDisplayName { get; init; } = string.Empty;
}

/// <summary>Profile review item returned by operator-paginated GET /api/faces/{faceId}/profiles/{userId}/reviews.</summary>
public sealed class FaceProfileReviewAdminItemDto
{
	public int Id { get; init; }
	public string AuthorUserId { get; init; } = string.Empty;
	public string Title { get; init; } = string.Empty;
	public string Text { get; init; } = string.Empty;
	public int Stars { get; init; }
	public DateTime CreatedAt { get; init; }
	public string AuthorDisplayName { get; init; } = string.Empty;
}
