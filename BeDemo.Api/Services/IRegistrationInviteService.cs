using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.Requests.OAuth;

namespace BeDemo.Api.Services;

/// <summary>
/// Application service for email-code registration (pending invites, mail, complete with OAuth tokens).
/// </summary>
public interface IRegistrationInviteService
{
    /// <summary>Public step 1 — always returns generic success to prevent email enumeration.</summary>
    Task<RegisterRequestResponseDto> RequestAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Load invite metadata for complete page; <paramref name="hash"/> is the mail link query value.</summary>
    Task<RegisterPrefillResponseDto?> GetPrefillAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>Public step 2 — creates user and returns tokens, or null on any validation/security failure.</summary>
    Task<RegisterCompleteResponseDto?> CompleteAsync(RegisterCompleteDto dto, CancellationToken cancellationToken = default);

    /// <summary>Resend mail for pending invite; rotates hash and code.</summary>
    Task<RegisterRequestResponseDto> ResendAsync(RegisterResendDto dto, CancellationToken cancellationToken = default);

    Task<RegistrationInviteListItemDto> CreateAdminInviteAsync(
        AdminCreateRegistrationInviteDto dto,
        string operatorUserId,
        CancellationToken cancellationToken = default);

    Task<AdminInviteListResponseDto> ListAdminInvitesAsync(
        AdminInviteListQuery query,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAdminInviteAsync(Guid id, CancellationToken cancellationToken = default);
}
