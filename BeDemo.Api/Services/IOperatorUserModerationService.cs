using BeDemo.Api.Models.DTOs.OperatorUsers;

namespace BeDemo.Api.Services;

public interface IOperatorUserModerationService
{
	Task<OperatorUserDetailDto?> GetDetailAsync(string targetUserId, CancellationToken cancellationToken = default);

	Task<(bool Success, string? Error, int StatusCode)> SetFaceRoleAsync(
		string operatorUserId,
		string targetUserId,
		int faceId,
		int userRoleId,
		string correlationId,
		CancellationToken cancellationToken = default);

	/// <summary>Self-service face role change for super-admin operator account (skips super-admin target guard).</summary>
	Task<(bool Success, string? Error, int StatusCode)> SetSelfFaceRoleAsync(
		string userId,
		int faceId,
		int userRoleId,
		string correlationId,
		CancellationToken cancellationToken = default);

	Task<(bool Success, string? Error, int StatusCode, bool AlreadyBanned)> GlobalBanAsync(
		string operatorUserId,
		string targetUserId,
		string reason,
		string correlationId,
		CancellationToken cancellationToken = default);

	Task<(bool Success, string? Error, int StatusCode)> GlobalUnbanAsync(
		string operatorUserId,
		string targetUserId,
		string correlationId,
		CancellationToken cancellationToken = default);

	Task<(bool Success, string? Error, int StatusCode, bool AlreadyBanned)> FaceBanAsync(
		string operatorUserId,
		string targetUserId,
		int faceId,
		string reason,
		string correlationId,
		CancellationToken cancellationToken = default);

	Task<(bool Success, string? Error, int StatusCode)> FaceUnbanAsync(
		string operatorUserId,
		string targetUserId,
		int faceId,
		string correlationId,
		CancellationToken cancellationToken = default);

	Task<(bool Success, string? Error, int StatusCode, int? MessageId)> SendPlatformMessageAsync(
		string operatorUserId,
		string targetUserId,
		string content,
		string correlationId,
		CancellationToken cancellationToken = default);
}
