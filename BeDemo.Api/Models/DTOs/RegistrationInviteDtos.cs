namespace BeDemo.Api.Models.DTOs;

/// <summary>HTTP contracts for email-code registration (`/api/oauth2/register/*` and admin invite APIs).</summary>

public sealed class RegisterRequestDto
{
    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Locale { get; set; }

    /// <summary>Optional: <c>mobile</c> to embed app deep link in mail.</summary>
    public string? Platform { get; set; }
}

public sealed class RegisterRequestResponseDto
{
    public string Message { get; set; } = "If this email can be used for registration, you will receive a message shortly.";
}

public sealed class RegisterCompleteDto
{
    public string Hash { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public bool? RememberMe { get; set; }
}

public sealed class RegisterCompleteResponseDto : OAuth2TokenResponse
{
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public sealed class RegisterResendDto
{
    public string Email { get; set; } = string.Empty;

    public string? Locale { get; set; }

    public string? Platform { get; set; }
}

public sealed class RegisterPrefillResponseDto
{
    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public bool Valid { get; set; }
}

public sealed class AdminCreateRegistrationInviteDto
{
    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Locale { get; set; }
}

public sealed class RegistrationInviteListItemDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }
}
