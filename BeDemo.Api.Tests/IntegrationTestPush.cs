using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorPush;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Tests;

/// <summary>
/// Restores operator push settings to env/bootstrap values in the shared in-memory test database.
/// </summary>
internal static class IntegrationTestPush
{
    internal const string TestFirebaseServiceAccountJson =
        """
        {
          "type": "service_account",
          "project_id": "demo-project",
          "private_key": "-----BEGIN PRIVATE KEY-----\nMIIB\n-----END PRIVATE KEY-----\n",
          "client_email": "firebase-adminsdk@test.iam.gserviceaccount.com"
        }
        """;

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
        var pushOptions = sp.GetRequiredService<IOptions<PushOptions>>().Value;
        var protector = sp.GetRequiredService<IOperatorPushSecretProtector>();
        var provider = sp.GetRequiredService<IOperatorPushSettingsProvider>();

        var row = await db.OperatorPushSystemSettings
            .SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);

        if (row == null)
        {
            row = new OperatorPushSystemSettings { Id = 1 };
            db.OperatorPushSystemSettings.Add(row);
        }

        row.Enabled = forceEnabled ?? pushOptions.Enabled;
        row.WorkerGrpcUrl = pushOptions.WorkerGrpcUrl;
        row.DefaultTitleLocKey = pushOptions.DefaultTitleLocKey;
        row.DefaultBodyLocKey = pushOptions.DefaultBodyLocKey;
        row.DefaultAndroidChannelId = pushOptions.DefaultAndroidChannelId;
        row.GrpcDeadlineSeconds = pushOptions.GrpcDeadlineSeconds;
        row.FirebaseProjectId = "demo-project";
        row.FirebaseServiceAccountJsonCiphertext = protector.Protect(TestFirebaseServiceAccountJson);
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedByUserId = null;
        row.WorkerAuthTokenCiphertext = string.IsNullOrWhiteSpace(pushOptions.WorkerAuthToken)
            ? null
            : protector.Protect(pushOptions.WorkerAuthToken.Trim());

        await db.SaveChangesAsync(cancellationToken);
        provider.InvalidateCache();
    }
}
