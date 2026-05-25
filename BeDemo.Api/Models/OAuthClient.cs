namespace BeDemo.Api.Models;

/// <summary>
/// Registered OAuth2 confidential client (O1). <see cref="SecretHash"/> is produced with <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{T}"/> over the plaintext secret — never store plaintext.
/// </summary>
public class OAuthClient
{
	public int Id { get; set; }

	/// <summary>Stable public identifier sent as <c>client_id</c>.</summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>ASP.NET Identity-style hash of <c>client_secret</c>.</summary>
	public string SecretHash { get; set; } = string.Empty;

	public bool IsActive { get; set; } = true;

	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
