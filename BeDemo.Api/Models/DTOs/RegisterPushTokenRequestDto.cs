namespace BeDemo.Api.Models.DTOs;

/// <summary>Request body for <c>POST /api/me/push-token</c> (mobile registers FCM token after login).</summary>
public sealed class RegisterPushTokenRequestDto
{
    public string RegistrationToken { get; set; } = string.Empty;

    /// <summary><c>ios</c> or <c>android</c> (lowercase).</summary>
    public string Platform { get; set; } = string.Empty;

    public string? InstallationId { get; set; }
}
