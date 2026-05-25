using System.Globalization;
using System.Security.Cryptography;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.Requests.OAuth;
using BeDemo.Api.Utils;
using BeDemo.Api.Validation.Rules;
using ManyFaces.Mailer.V1;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Services.OperatorMail;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// Core business logic for two-step email-code registration:
/// pending <see cref="RegistrationInvite"/> rows, mail via gRPC, and account creation with OAuth tokens on complete.
/// </summary>
public sealed class RegistrationInviteService : IRegistrationInviteService
{
	/// <summary>Always returned on request/resend success paths to avoid email enumeration.</summary>
	private static readonly RegisterRequestResponseDto GenericOk = new();

	private readonly ApplicationDbContext _context;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly IMailerWorkerClient _mailer;
	private readonly IOAuthAccessTokenFactory _accessTokens;
	private readonly IOAuthRefreshTokenStore _refreshTokens;
	private readonly IOAuthClientValidator _clientValidator;
	private readonly IUserRegistrationProvisioner _provisioner;
	private readonly IOptions<RegistrationInviteOptions> _inviteOptions;
	private readonly IOperatorMailSettingsProvider _mailSettings;
	private readonly ILogger<RegistrationInviteService> _logger;

	public RegistrationInviteService(
		ApplicationDbContext context,
		UserManager<ApplicationUser> userManager,
		IMailerWorkerClient mailer,
		IOAuthAccessTokenFactory accessTokens,
		IOAuthRefreshTokenStore refreshTokens,
		IOAuthClientValidator clientValidator,
		IUserRegistrationProvisioner provisioner,
		IOptions<RegistrationInviteOptions> inviteOptions,
		IOperatorMailSettingsProvider mailSettings,
		ILogger<RegistrationInviteService> logger)
	{
		_context = context;
		_userManager = userManager;
		_mailer = mailer;
		_accessTokens = accessTokens;
		_refreshTokens = refreshTokens;
		_clientValidator = clientValidator;
		_provisioner = provisioner;
		_inviteOptions = inviteOptions;
		_mailSettings = mailSettings;
		_logger = logger;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Step 1 of public signup: if email is not already a user, revoke any prior pending row and create a new invite + mail.
	/// Existing users get the same generic response without mail (anti-enumeration).
	/// </remarks>
	public async Task<RegisterRequestResponseDto> RequestAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default)
	{
		var normalized = _userManager.NormalizeEmail(dto.Email);
		if (string.IsNullOrEmpty(normalized))
		{
			return GenericOk;
		}

		if (await _userManager.FindByEmailAsync(dto.Email).ConfigureAwait(false) != null)
		{
			return GenericOk;
		}

		await ReplacePendingAndSendAsync(
			dto.Email,
			normalized,
			dto.FirstName,
			dto.LastName,
			await ResolveLocaleAsync(dto.Locale, cancellationToken),
			createdByUserId: null,
			preferMobileLink: IsMobilePlatform(dto.Platform),
			cancellationToken).ConfigureAwait(false);

		return GenericOk;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Re-issues mail for an existing pending invite: rotates link hash + verification code (old link stops working).
	/// </remarks>
	public async Task<RegisterRequestResponseDto> ResendAsync(RegisterResendDto dto, CancellationToken cancellationToken = default)
	{
		var normalized = _userManager.NormalizeEmail(dto.Email);
		if (string.IsNullOrEmpty(normalized))
		{
			return GenericOk;
		}

		if (await _userManager.FindByEmailAsync(dto.Email).ConfigureAwait(false) != null)
		{
			return GenericOk;
		}

		var pending = await FindActivePendingAsync(normalized, cancellationToken).ConfigureAwait(false);
		if (pending == null)
		{
			return GenericOk;
		}

		await ReplacePendingAndSendAsync(
			pending.Email,
			normalized,
			pending.FirstName,
			pending.LastName,
			await ResolveLocaleAsync(dto.Locale ?? pending.Locale, cancellationToken),
			createdByUserId: pending.CreatedByUserId,
			preferMobileLink: IsMobilePlatform(dto.Platform),
			cancellationToken).ConfigureAwait(false);

		return GenericOk;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Used by complete-registration UI on mount: exposes email/names and whether invite is still valid.
	/// Never returns the verification code.
	/// </remarks>
	public async Task<RegisterPrefillResponseDto?> GetPrefillAsync(string hash, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(hash))
		{
			return null;
		}

		var invite = await _context.RegistrationInvites
			.AsNoTracking()
			.FirstOrDefaultAsync(i => i.LinkHash == hash.Trim(), cancellationToken)
			.ConfigureAwait(false);

		if (invite == null)
		{
			return new RegisterPrefillResponseDto { Valid = false };
		}

		var valid = IsInviteActive(invite);
		return new RegisterPrefillResponseDto
		{
			Email = invite.Email,
			FirstName = invite.FirstName,
			LastName = invite.LastName,
			ExpiresAtUtc = invite.ExpiresAtUtc,
			Valid = valid,
		};
	}

	/// <inheritdoc />
	/// <remarks>
	/// Step 2: validates hash+code pair, creates Identity user (EmailConfirmed=true), provisions profiles,
	/// marks invite consumed, and returns OAuth2 tokens so clients can skip a separate login call.
	/// </remarks>
	public async Task<RegisterCompleteResponseDto?> CompleteAsync(RegisterCompleteDto dto, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(dto.Hash) || string.IsNullOrWhiteSpace(dto.Code))
		{
			return null;
		}

		if (!await _clientValidator.ValidateAsync(dto.ClientId, dto.ClientSecret).ConfigureAwait(false))
		{
			return null;
		}

		// InMemory test DB does not support transactions; relational DB uses a transaction to prevent double-submit races.
		var useTransaction = _context.Database.IsRelational();
		await using var tx = useTransaction
			? await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
			: null;

		var invite = await _context.RegistrationInvites
			.FirstOrDefaultAsync(i => i.LinkHash == dto.Hash.Trim(), cancellationToken)
			.ConfigureAwait(false);

		if (invite == null || !IsInviteActive(invite))
		{
			if (tx != null)
			{
				await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
			}

			return null;
		}

		var o = _inviteOptions.Value;
		if (invite.FailedAttemptCount >= o.MaxAttempts)
		{
			if (tx != null)
			{
				await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
			}

			return null;
		}

		var codeHash = RegistrationInviteCrypto.HashCode(dto.Code, o.HmacPepper);
		if (!RegistrationInviteCrypto.FixedTimeEqualsHash(invite.CodeHash, codeHash))
		{
			invite.FailedAttemptCount++;
			await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			if (tx != null)
			{
				await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
			}

			return null;
		}

		var userRole = await _context.UserRoles
			.FirstOrDefaultAsync(r => r.Name == UserRole.GlobalRoleNames.User, cancellationToken)
			.ConfigureAwait(false);
		if (userRole == null)
		{
			if (tx != null)
			{
				await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
			}

			return null;
		}

		var user = new ApplicationUser
		{
			UserName = invite.Email,
			Email = invite.Email,
			EmailConfirmed = true,
			FirstName = dto.FirstName ?? invite.FirstName,
			LastName = dto.LastName ?? invite.LastName,
			UserRoleId = userRole.Id,
		};

		var createResult = await _userManager.CreateAsync(user, dto.Password).ConfigureAwait(false);
		if (!createResult.Succeeded)
		{
			if (tx != null)
			{
				await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
			}

			return null;
		}

		await _provisioner.ProvisionNewUserAsync(user, cancellationToken).ConfigureAwait(false);

		invite.ConsumedAtUtc = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		var useRememberMe = dto.RememberMe == true;
		var (accessJwt, minutes) = await _accessTokens.CreateAsync(user, useRememberMe).ConfigureAwait(false);
		var refreshPlain = GenerateOpaqueRefreshToken();
		await _refreshTokens.CreateAsync(user.Id, refreshPlain, useRememberMe).ConfigureAwait(false);

		if (tx != null)
		{
			await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
		}

		return new RegisterCompleteResponseDto
		{
			AccessToken = accessJwt,
			TokenType = "Bearer",
			ExpiresIn = minutes * 60,
			RefreshToken = refreshPlain,
			UserId = user.Id,
			Email = user.Email ?? invite.Email,
		};
	}

	private static string GenerateOpaqueRefreshToken()
	{
		var randomBytes = new byte[64];
		RandomNumberGenerator.Fill(randomBytes);
		return Convert.ToBase64String(randomBytes);
	}

	/// <summary>
	/// Single path for creating/replacing a pending invite: revoke prior pending rows for the email,
	/// insert new hash+code, then send templated mail (if Mail:Enabled).
	/// </summary>
	private async Task<RegistrationInvite> ReplacePendingAndSendAsync(
		string email,
		string normalizedEmail,
		string? firstName,
		string? lastName,
		string locale,
		string? createdByUserId,
		bool preferMobileLink,
		CancellationToken cancellationToken)
	{
		var existing = await _context.RegistrationInvites
			.Where(i => i.NormalizedEmail == normalizedEmail && i.ConsumedAtUtc == null && i.RevokedAtUtc == null)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		if (existing.Count > 0)
		{
			foreach (var row in existing)
			{
				row.RevokedAtUtc = DateTime.UtcNow;
			}
		}

		var o = _inviteOptions.Value;
		var plainCode = RegistrationInviteCrypto.GenerateVerificationCode(o.CodeLength);
		var linkHash = RegistrationInviteCrypto.GenerateLinkHash();
		var invite = new RegistrationInvite
		{
			Id = Guid.NewGuid(),
			Email = email,
			NormalizedEmail = normalizedEmail,
			FirstName = firstName,
			LastName = lastName,
			LinkHash = linkHash,
			CodeHash = RegistrationInviteCrypto.HashCode(plainCode, o.HmacPepper),
			FailedAttemptCount = 0,
			ExpiresAtUtc = DateTime.UtcNow.AddMinutes(o.ExpiryMinutes),
			CreatedAtUtc = DateTime.UtcNow,
			CreatedByUserId = createdByUserId,
			Locale = locale,
		};

		_context.RegistrationInvites.Add(invite);
		await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		await TrySendMailAsync(invite, plainCode, preferMobileLink, cancellationToken).ConfigureAwait(false);
		return invite;
	}

	/// <summary>
	/// Sends <see cref="MailTemplateIds.AccountRegistrationCode"/> via gRPC; plaintext code exists only in this call stack.
	/// </summary>
	private async Task TrySendMailAsync(
		RegistrationInvite invite,
		string plainCode,
		bool preferMobileLink,
		CancellationToken cancellationToken)
	{
		var mail = await _mailSettings.GetAsync(cancellationToken).ConfigureAwait(false);
		if (!mail.IsSendAllowed)
		{
			_logger.LogWarning(
				"Mail worker disabled; registration invite created but email not sent ({EmailHint})",
				PiiLogRedaction.FormatEmailForLog(invite.Email, invite.Id));
			return;
		}

		var actionLink = BuildActionLink(invite, preferMobileLink, mail);
		var display = PickDisplayName(invite);
		var request = new SendTemplatedEmailRequest();
		request.To.Add(invite.Email);
		request.TemplateId = MailTemplateIds.AccountRegistrationCode;
		request.Locale = invite.Locale;
		request.Params["action_link"] = actionLink;
		request.Params["registration_code"] = plainCode;
		request.Params["user_name"] = display;
		request.Params["expiry_minutes"] = _inviteOptions.Value.ExpiryMinutes.ToString(CultureInfo.InvariantCulture);
		request.IdempotencyKey = $"registration:{invite.Id:N}:{invite.LinkHash[..Math.Min(8, invite.LinkHash.Length)]}";

		await _mailer.SendTemplatedEmailAsync(request, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Portal HTTPS link or mobile deep link; hash is URL-encoded as the sole query parameter.
	/// </summary>
	private static string BuildActionLink(RegistrationInvite invite, bool preferMobileLink, OperatorMailSettingsValues link)
	{
		var hash = Uri.EscapeDataString(invite.LinkHash);
		if (preferMobileLink && link.PreferMobileDeepLinkWhenPlatformMobile)
		{
			return $"{link.MobileDeepLinkBase.TrimEnd('/')}?hash={hash}";
		}

		var locale = invite.Locale.Split('-')[0];
		var path = link.CompleteRegistrationPathTemplate
			.Replace("{locale}", locale, StringComparison.OrdinalIgnoreCase);
		var baseUrl = link.PortalPublicBaseUrl.TrimEnd('/');
		return $"{baseUrl}{path}?hash={hash}";
	}

	private static string PickDisplayName(RegistrationInvite invite)
	{
		if (!string.IsNullOrWhiteSpace(invite.FirstName))
		{
			return invite.FirstName.Trim();
		}

		var at = invite.Email.IndexOf('@');
		return at > 0 ? invite.Email[..at] : invite.Email;
	}

	private async Task<RegistrationInvite?> FindActivePendingAsync(string normalizedEmail, CancellationToken cancellationToken)
	{
		return await _context.RegistrationInvites
			.Where(i => i.NormalizedEmail == normalizedEmail && i.ConsumedAtUtc == null && i.RevokedAtUtc == null)
			.OrderByDescending(i => i.CreatedAtUtc)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);
	}

	private static bool IsInviteActive(RegistrationInvite invite) =>
		invite.ConsumedAtUtc == null
		&& invite.RevokedAtUtc == null
		&& invite.ExpiresAtUtc > DateTime.UtcNow;

	private async Task<string> ResolveLocaleAsync(string? locale, CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(locale))
			return locale.Trim();

		var current = CultureInfo.CurrentUICulture.Name;
		if (!string.IsNullOrWhiteSpace(current))
			return current;

		var mail = await _mailSettings.GetAsync(cancellationToken).ConfigureAwait(false);
		return mail.DefaultLocale;
	}

	private static bool IsMobilePlatform(string? platform) =>
		string.Equals(platform, "mobile", StringComparison.OrdinalIgnoreCase);
}
