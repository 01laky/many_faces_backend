namespace BeDemo.Api.Models;

/// <summary>
/// Singleton platform row: operator-editable push settings (platform + Firebase FCM) for admin Settings.
/// Secrets are stored encrypted via <see cref="Services.OperatorPush.IOperatorPushSecretProtector"/>.
/// </summary>
public class OperatorPushSystemSettings
{
	/// <summary>Always <c>1</c> — platform-wide singleton.</summary>
	public int Id { get; set; } = 1;

	public bool Enabled { get; set; }

	public string? WorkerGrpcUrl { get; set; }

	public string? WorkerAuthTokenCiphertext { get; set; }

	public string? FirebaseProjectId { get; set; }

	public string? FirebaseServiceAccountJsonCiphertext { get; set; }

	public string DefaultTitleLocKey { get; set; } = "push_test_title";

	public string DefaultBodyLocKey { get; set; } = "push_test_body";

	public string? DefaultAndroidChannelId { get; set; }

	public int GrpcDeadlineSeconds { get; set; } = 15;

	public DateTime UpdatedAtUtc { get; set; }

	public string? UpdatedByUserId { get; set; }
}
