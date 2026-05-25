using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.Admin;
using BeDemo.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorMail;

/// <summary>
/// L1 cache for operator mail settings. DB row overrides env/bootstrap on read; insert-on-first-read seeds from <see cref="MailOptions"/>.
/// </summary>
public sealed class OperatorMailSettingsService : IOperatorMailSettingsProvider
{
    private const string MemoryCacheKey = "OperatorMail:SystemSettings";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly IHostEnvironment _environment;
    private readonly MailOptions _mailOptions;
    private readonly MailRegistrationLinkOptions _linkOptions;
    private readonly IOperatorMailSecretProtector _secretProtector;

    public OperatorMailSettingsService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        IHostEnvironment environment,
        IOptions<MailOptions> mailOptions,
        IOptions<MailRegistrationLinkOptions> linkOptions,
        IOperatorMailSecretProtector secretProtector)
    {
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _environment = environment;
        _mailOptions = mailOptions.Value;
        _linkOptions = linkOptions.Value;
        _secretProtector = secretProtector;
    }

    /// <inheritdoc />
    public async Task<OperatorMailSettingsValues> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(MemoryCacheKey, out OperatorMailSettingsValues? cached) && cached != null)
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.OperatorMailSystemSettings
            .SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);

        if (row == null)
        {
            row = CreateBootstrapRow();
            db.OperatorMailSystemSettings.Add(row);
            await db.SaveChangesAsync(cancellationToken);
        }

        var values = ToValues(row);
        Cache(values);
        return values;
    }

    /// <inheritdoc />
    public async Task<OperatorMailSettingsValues> SetAsync(
        OperatorMailSettingsValues values,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.OperatorMailSystemSettings.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);
        if (row == null)
        {
            row = CreateBootstrapRow();
            db.OperatorMailSystemSettings.Add(row);
        }

        ApplyValues(row, values);
        await db.SaveChangesAsync(cancellationToken);

        Cache(values);
        return values;
    }

    /// <inheritdoc />
    public AdminMailSettingsDto ToDto(OperatorMailSettingsValues values) => new()
    {
        Enabled = values.Enabled,
        DefaultLocale = values.DefaultLocale,
        WorkerGrpcUrl = values.WorkerGrpcUrl,
        HasWorkerAuthToken = values.HasWorkerAuthToken,
        Smtp = new AdminMailSmtpSettingsDto
        {
            Host = values.SmtpHost,
            Port = values.SmtpPort,
            StartTls = values.SmtpStartTls,
            User = values.SmtpUser,
            HasPassword = values.HasSmtpPassword,
        },
        From = new AdminMailFromSettingsDto
        {
            Email = values.FromEmail,
            DisplayName = values.FromDisplayName,
        },
        RegistrationLinks = new AdminMailRegistrationLinksDto
        {
            PortalPublicBaseUrl = values.PortalPublicBaseUrl,
            CompleteRegistrationPathTemplate = values.CompleteRegistrationPathTemplate,
            MobileDeepLinkBase = values.MobileDeepLinkBase,
            PreferMobileDeepLinkWhenPlatformMobile = values.PreferMobileDeepLinkWhenPlatformMobile,
        },
        EffectiveStatus = values.EffectiveStatus,
        UpdatedAtUtc = values.UpdatedAtUtc,
        UpdatedByUserId = values.UpdatedByUserId,
    };

    /// <inheritdoc />
    public void InvalidateCache() => _memoryCache.Remove(MemoryCacheKey);

    private OperatorMailSystemSettings CreateBootstrapRow()
    {
        var bootstrap = ResolveBootstrapEnabled();
        var row = new OperatorMailSystemSettings
        {
            Id = 1,
            Enabled = bootstrap,
            DefaultLocale = _mailOptions.DefaultLocale,
            WorkerGrpcUrl = _mailOptions.WorkerGrpcUrl,
            SmtpHost = _mailOptions.Smtp.Host,
            SmtpPort = _mailOptions.Smtp.Port,
            SmtpStartTls = _mailOptions.Smtp.StartTls,
            SmtpUser = _mailOptions.Smtp.User,
            FromEmail = _mailOptions.From.Email,
            FromDisplayName = _mailOptions.From.DisplayName,
            PortalPublicBaseUrl = _linkOptions.PortalPublicBaseUrl,
            CompleteRegistrationPathTemplate = _linkOptions.CompleteRegistrationPathTemplate,
            MobileDeepLinkBase = _linkOptions.MobileDeepLinkBase,
            PreferMobileDeepLinkWhenPlatformMobile = _linkOptions.PreferMobileDeepLinkWhenPlatformMobile,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        if (!string.IsNullOrWhiteSpace(_mailOptions.WorkerAuthToken))
            row.WorkerAuthTokenCiphertext = _secretProtector.Protect(_mailOptions.WorkerAuthToken.Trim());

        if (!string.IsNullOrWhiteSpace(_mailOptions.Smtp.Password))
            row.SmtpPasswordCiphertext = _secretProtector.Protect(_mailOptions.Smtp.Password);

        return row;
    }

    private bool ResolveBootstrapEnabled()
    {
        if (_environment.IsEnvironment("Testing"))
            return _mailOptions.Enabled;

        return _mailOptions.Enabled;
    }

    private OperatorMailSettingsValues ToValues(OperatorMailSystemSettings row) => new(
        row.Enabled,
        row.DefaultLocale,
        row.WorkerGrpcUrl,
        UnprotectOrNull(row.WorkerAuthTokenCiphertext),
        row.SmtpHost,
        row.SmtpPort,
        row.SmtpStartTls,
        row.SmtpUser,
        UnprotectOrNull(row.SmtpPasswordCiphertext),
        row.FromEmail,
        row.FromDisplayName,
        row.PortalPublicBaseUrl,
        row.CompleteRegistrationPathTemplate,
        row.MobileDeepLinkBase,
        row.PreferMobileDeepLinkWhenPlatformMobile,
        row.UpdatedAtUtc,
        row.UpdatedByUserId);

    private void ApplyValues(OperatorMailSystemSettings row, OperatorMailSettingsValues values)
    {
        row.Enabled = values.Enabled;
        row.DefaultLocale = values.DefaultLocale;
        row.WorkerGrpcUrl = values.WorkerGrpcUrl;
        row.WorkerAuthTokenCiphertext = string.IsNullOrWhiteSpace(values.WorkerAuthTokenPlaintext)
            ? null
            : _secretProtector.Protect(values.WorkerAuthTokenPlaintext.Trim());
        row.SmtpHost = values.SmtpHost;
        row.SmtpPort = values.SmtpPort;
        row.SmtpStartTls = values.SmtpStartTls;
        row.SmtpUser = values.SmtpUser;
        row.SmtpPasswordCiphertext = string.IsNullOrWhiteSpace(values.SmtpPasswordPlaintext)
            ? null
            : _secretProtector.Protect(values.SmtpPasswordPlaintext);
        row.FromEmail = values.FromEmail;
        row.FromDisplayName = values.FromDisplayName;
        row.PortalPublicBaseUrl = values.PortalPublicBaseUrl;
        row.CompleteRegistrationPathTemplate = values.CompleteRegistrationPathTemplate;
        row.MobileDeepLinkBase = values.MobileDeepLinkBase;
        row.PreferMobileDeepLinkWhenPlatformMobile = values.PreferMobileDeepLinkWhenPlatformMobile;
        row.UpdatedAtUtc = values.UpdatedAtUtc;
        row.UpdatedByUserId = values.UpdatedByUserId;
    }

    private string? UnprotectOrNull(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
            return null;

        try
        {
            return _secretProtector.Unprotect(ciphertext);
        }
        catch
        {
            return null;
        }
    }

    private void Cache(OperatorMailSettingsValues values)
    {
        _memoryCache.Set(
            MemoryCacheKey,
            values,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            });
    }
}
