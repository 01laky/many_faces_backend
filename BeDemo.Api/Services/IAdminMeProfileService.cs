using BeDemo.Api.Models.DTOs.Admin;
using BeDemo.Api.Models.Requests.Admin;

namespace BeDemo.Api.Services;

public interface IAdminMeProfileService
{
	Task<AdminMeProfileDto?> GetProfileAsync(string userId, string scheme, string host, CancellationToken cancellationToken = default);

	Task<(AdminMeProfileDto? Profile, string? Error, int StatusCode, bool EmailChanged)> UpdateProfileAsync(
		string userId,
		UpdateAdminMeProfileRequest request,
		string scheme,
		string host,
		string locale,
		string correlationId,
		CancellationToken cancellationToken = default);

	Task<(string? Error, int StatusCode)> UpdatePasswordAsync(
		string userId,
		UpdateAdminMePasswordRequest request,
		CancellationToken cancellationToken = default);

	Task<(bool Success, string? Error, int StatusCode)> SetSelfFaceRoleAsync(
		string userId,
		int faceId,
		int userRoleId,
		string correlationId,
		CancellationToken cancellationToken = default);

	Task<(bool Success, string? Error, int StatusCode)> ResendEmailConfirmationAsync(
		string userId,
		string scheme,
		string host,
		string locale,
		CancellationToken cancellationToken = default);

	Task<(bool Success, string? Error, int StatusCode)> ConfirmEmailAsync(
		string userId,
		string token,
		CancellationToken cancellationToken = default);
}
