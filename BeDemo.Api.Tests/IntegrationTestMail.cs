using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorMail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Tests;

/// <summary>
/// Restores operator mail settings to env/bootstrap values in the shared in-memory test database.
/// Admin mail settings tests may disable mail; registration and infra tests expect send-ready defaults.
/// </summary>
internal static class IntegrationTestMail
{
	public static Task ResetToBootstrapAsync(
		CustomWebApplicationFactory<Program> factory,
		bool? forceEnabled = null,
		CancellationToken cancellationToken = default) =>
		ResetToBootstrapAsync(factory.Services, forceEnabled, cancellationToken);

	public static async Task ResetToBootstrapAsync(
		IServiceProvider services,
		bool? forceEnabled = null,
		CancellationToken cancellationToken = default)
	{
		using var scope = services.CreateScope();
		var sp = scope.ServiceProvider;
		var db = sp.GetRequiredService<ApplicationDbContext>();
		var mailOptions = sp.GetRequiredService<IOptions<MailOptions>>().Value;
		var linkOptions = sp.GetRequiredService<IOptions<MailRegistrationLinkOptions>>().Value;
		var protector = sp.GetRequiredService<IOperatorMailSecretProtector>();
		var provider = sp.GetRequiredService<IOperatorMailSettingsProvider>();

		var row = await db.OperatorMailSystemSettings
			.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);

		if (row == null)
		{
			row = new OperatorMailSystemSettings { Id = 1 };
			db.OperatorMailSystemSettings.Add(row);
		}

		row.Enabled = forceEnabled ?? mailOptions.Enabled;
		row.DefaultLocale = mailOptions.DefaultLocale;
		row.WorkerGrpcUrl = mailOptions.WorkerGrpcUrl;
		row.SmtpHost = mailOptions.Smtp.Host;
		row.SmtpPort = mailOptions.Smtp.Port;
		row.SmtpStartTls = mailOptions.Smtp.StartTls;
		row.SmtpUser = mailOptions.Smtp.User;
		row.FromEmail = mailOptions.From.Email;
		row.FromDisplayName = mailOptions.From.DisplayName;
		row.PortalPublicBaseUrl = linkOptions.PortalPublicBaseUrl;
		row.CompleteRegistrationPathTemplate = linkOptions.CompleteRegistrationPathTemplate;
		row.MobileDeepLinkBase = linkOptions.MobileDeepLinkBase;
		row.PreferMobileDeepLinkWhenPlatformMobile = linkOptions.PreferMobileDeepLinkWhenPlatformMobile;
		row.UpdatedAtUtc = DateTime.UtcNow;
		row.UpdatedByUserId = null;
		row.WorkerAuthTokenCiphertext = string.IsNullOrWhiteSpace(mailOptions.WorkerAuthToken)
			? null
			: protector.Protect(mailOptions.WorkerAuthToken.Trim());
		row.SmtpPasswordCiphertext = string.IsNullOrWhiteSpace(mailOptions.Smtp.Password)
			? null
			: protector.Protect(mailOptions.Smtp.Password);

		await db.SaveChangesAsync(cancellationToken);
		provider.InvalidateCache();
	}
}
