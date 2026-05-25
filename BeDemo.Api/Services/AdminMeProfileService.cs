using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.Admin;
using BeDemo.Api.Models.DTOs.OperatorUsers;
using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Utils;
using ManyFaces.Mailer.V1;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;

namespace BeDemo.Api.Services;

/// <summary>Super-admin self-service profile on the admin face (identity, password, face roles, email confirm).</summary>
public sealed class AdminMeProfileService : IAdminMeProfileService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOperatorUserModerationService _moderation;
    private readonly IOAuthRefreshTokenStore _refreshTokens;
    private readonly IUploadSignedUrlService _uploadUrls;
    private readonly IMailerWorkerClient _mailerWorker;
    private readonly IOptions<MailOptions> _mailOptions;
    private readonly ILogger<AdminMeProfileService> _logger;

    public AdminMeProfileService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IOperatorUserModerationService moderation,
        IOAuthRefreshTokenStore refreshTokens,
        IUploadSignedUrlService uploadUrls,
        IMailerWorkerClient mailerWorker,
        IOptions<MailOptions> mailOptions,
        ILogger<AdminMeProfileService> logger)
    {
        _context = context;
        _userManager = userManager;
        _moderation = moderation;
        _refreshTokens = refreshTokens;
        _uploadUrls = uploadUrls;
        _mailerWorker = mailerWorker;
        _mailOptions = mailOptions;
        _logger = logger;
    }

    public async Task<AdminMeProfileDto?> GetProfileAsync(
        string userId,
        string scheme,
        string host,
        CancellationToken cancellationToken = default)
    {
        var detail = await _moderation.GetDetailAsync(userId, cancellationToken);
        if (detail == null)
            return null;

        var avatarPath = await _context.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.AvatarUrl)
            .FirstOrDefaultAsync(cancellationToken);

        return MapFromDetail(detail, avatarPath, scheme, host);
    }

    public async Task<(AdminMeProfileDto? Profile, string? Error, int StatusCode, bool EmailChanged)> UpdateProfileAsync(
        string userId,
        UpdateAdminMeProfileRequest request,
        string scheme,
        string host,
        string locale,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (null, "User not found", StatusCodes.Status404NotFound, false);

        var emailChanged = false;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalized = request.Email.Trim().ToLowerInvariant();
            if (!string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByEmailAsync(normalized);
                if (existing != null && existing.Id != userId)
                    return (null, "Email is already in use", StatusCodes.Status409Conflict, false);

                user.Email = normalized;
                user.UserName = normalized;
                user.EmailConfirmed = false;
                emailChanged = true;
            }
        }

        if (request.FirstName != null)
            user.FirstName = request.FirstName.Trim().Length > 0 ? request.FirstName.Trim() : null;
        if (request.LastName != null)
            user.LastName = request.LastName.Trim().Length > 0 ? request.LastName.Trim() : null;

        if (emailChanged)
        {
            user.AccessTokenVersion++;
            await _refreshTokens.RevokeAllActiveForUserAsync(userId, cancellationToken);
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var msg = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            return (null, msg, StatusCodes.Status400BadRequest, false);
        }

        if (emailChanged)
        {
            var mailError = await TrySendEmailConfirmationAsync(user, scheme, host, locale, cancellationToken);
            if (mailError != null)
            {
                _logger.LogWarning(
                    "Admin profile email updated but confirmation mail failed for user {UserId}: {Error}",
                    userId,
                    mailError);
            }
        }

        var profile = await GetProfileAsync(userId, scheme, host, cancellationToken);
        return (profile, null, StatusCodes.Status200OK, emailChanged);
    }

    public async Task<(string? Error, int StatusCode)> UpdatePasswordAsync(
        string userId,
        UpdateAdminMePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return ("User not found", StatusCodes.Status404NotFound);

        if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
            return ("Current password is incorrect", StatusCodes.Status400BadRequest);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            var msg = string.Join("; ", resetResult.Errors.Select(e => e.Description));
            return (msg, StatusCodes.Status400BadRequest);
        }

        user.AccessTokenVersion++;
        await _userManager.UpdateAsync(user);
        await _refreshTokens.RevokeAllActiveForUserAsync(userId, cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public Task<(bool Success, string? Error, int StatusCode)> SetSelfFaceRoleAsync(
        string userId,
        int faceId,
        int userRoleId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        _moderation.SetSelfFaceRoleAsync(userId, faceId, userRoleId, correlationId, cancellationToken);

    public async Task<(bool Success, string? Error, int StatusCode)> ResendEmailConfirmationAsync(
        string userId,
        string scheme,
        string host,
        string locale,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, "User not found", StatusCodes.Status404NotFound);
        if (user.EmailConfirmed)
            return (true, null, StatusCodes.Status200OK);
        if (string.IsNullOrEmpty(user.Email))
            return (false, "Account has no email address", StatusCodes.Status400BadRequest);

        var mailError = await TrySendEmailConfirmationAsync(user, scheme, host, locale, cancellationToken);
        if (mailError != null)
            return (false, mailError, StatusCodes.Status400BadRequest);

        return (true, null, StatusCodes.Status200OK);
    }

    public async Task<(bool Success, string? Error, int StatusCode)> ConfirmEmailAsync(
        string userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, "Invalid confirmation link", StatusCodes.Status400BadRequest);

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return (false, "Invalid or expired confirmation link", StatusCodes.Status400BadRequest);

        return (true, null, StatusCodes.Status200OK);
    }

    private async Task<string?> TrySendEmailConfirmationAsync(
        ApplicationUser user,
        string scheme,
        string host,
        string locale,
        CancellationToken cancellationToken)
    {
        if (!_mailOptions.Value.Enabled)
            return "Mail worker is disabled or misconfigured (Mail:Enabled / Mail:WorkerGrpcUrl).";

        if (string.IsNullOrEmpty(user.Email))
            return "Account has no email address.";

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var facePrefix = "/" + Routing.ConvertToKebabCase("admin").Trim('/');
        var confirmUrl =
            $"{scheme}://{host}{facePrefix}/api/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

        var display = string.IsNullOrWhiteSpace(user.FirstName)
            ? user.Email!.Split('@')[0]
            : $"{user.FirstName} {user.LastName}".Trim();

        var request = new SendTemplatedEmailRequest();
        request.To.Add(user.Email!);
        request.TemplateId = MailTemplateIds.IdentityEmailConfirm;
        request.Locale = string.IsNullOrWhiteSpace(locale) ? "en" : locale;
        request.Params["action_link"] = confirmUrl;
        request.Params["user_name"] = display;

        var response = await _mailerWorker.SendTemplatedEmailAsync(request, cancellationToken);
        return response is null ? "Mail worker is disabled or misconfigured." : null;
    }

    private AdminMeProfileDto MapFromDetail(OperatorUserDetailDto detail, string? avatarPath, string scheme, string host) =>
        new()
        {
            Id = detail.Id,
            Email = detail.Email,
            FirstName = detail.FirstName,
            LastName = detail.LastName,
            CreatedAt = detail.CreatedAt,
            GlobalRole = new AdminMeGlobalRoleDto
            {
                UserRoleId = detail.GlobalRole.UserRoleId,
                Name = detail.GlobalRole.Name,
            },
            EmailConfirmed = detail.Badges.EmailConfirmed,
            GlobalAvatarUrl = _uploadUrls.ToAbsoluteSignedUrl(avatarPath, scheme, host),
            Faces = detail.Faces.Select(f => new AdminMeFaceRowDto
            {
                FaceId = f.FaceId,
                FaceIndex = f.FaceIndex,
                FaceTitle = f.FaceTitle,
                UserRoleId = f.UserRoleId,
                RoleName = f.RoleName,
                IsActiveParticipant = f.IsActiveParticipant,
            }).ToList(),
        };
}
