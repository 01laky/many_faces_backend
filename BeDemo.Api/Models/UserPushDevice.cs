using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

/// <summary>
/// One row per mobile installation / FCM registration token for a Many Faces user (many_faces_mobile registers via HTTPS).
/// </summary>
public sealed class UserPushDevice
{
	public int Id { get; set; }

	[Required]
	[MaxLength(450)]
	public string UserId { get; set; } = string.Empty;

	/// <summary>Lowercase platform discriminator: <c>ios</c> or <c>android</c>.</summary>
	[Required]
	[MaxLength(32)]
	public string Platform { get; set; } = string.Empty;

	/// <summary>FCM registration token (treat as secret in transit; never log full value).</summary>
	[Required]
	[MaxLength(512)]
	public string RegistrationToken { get; set; } = string.Empty;

	/// <summary>Optional Expo / device installation id used to upsert the same physical device.</summary>
	[MaxLength(200)]
	public string? InstallationId { get; set; }

	public DateTime CreatedAtUtc { get; set; }

	public DateTime UpdatedAtUtc { get; set; }

	public DateTime? LastSeenAtUtc { get; set; }
}
