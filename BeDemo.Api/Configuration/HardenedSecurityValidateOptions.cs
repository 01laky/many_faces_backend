using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Configuration;

/// <summary>
/// BSH3 — fail-fast validation when running under the <c>Hardened</c> environment profile.
/// </summary>
public sealed class HardenedSecurityValidateOptions : IValidateOptions<HardenedSecurityOptions>
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public HardenedSecurityValidateOptions(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, HardenedSecurityOptions options)
    {
        if (!_environment.IsEnvironment("Hardened"))
            return ValidateOptionsResult.Success;

        var errors = new List<string>();

        if (options.RejectPlaceholderSecrets)
        {
            RejectPlaceholder(_configuration["Uploads:SigningSecret"], "Uploads:SigningSecret", errors);
            RejectPlaceholder(_configuration["RegistrationInvite:HmacPepper"], "RegistrationInvite:HmacPepper", errors);
            RejectPlaceholder(_configuration["OAuth2:ClientSecret"], "OAuth2:ClientSecret", errors);
        }

        if (options.EnforceJwtSigningPem)
        {
            var pem = _configuration["Jwt:SigningPemPath"];
            if (string.IsNullOrWhiteSpace(pem))
                errors.Add("Jwt:SigningPemPath is required in Hardened profile.");
            else if (!File.Exists(pem))
                errors.Add($"Jwt:SigningPemPath file not found: {pem}");
        }

        if (options.EnforceWorkerTlsAndTokens)
        {
            ValidateWorkerSection("Search", errors);
            ValidateWorkerSection("Push", errors);
            ValidateWorkerSection("Mail", errors);
            ValidateAiSection(errors);
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private void ValidateWorkerSection(string section, List<string> errors)
    {
        if (!_configuration.GetValue($"{section}:Enabled", false))
            return;

        var url = _configuration[$"{section}:WorkerGrpcUrl"];
        if (string.IsNullOrWhiteSpace(url))
        {
            errors.Add($"{section}:WorkerGrpcUrl is required when {section}:Enabled is true.");
            return;
        }

        if (!url.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            errors.Add($"{section}:WorkerGrpcUrl must use https:// in Hardened profile.");

        if (string.IsNullOrWhiteSpace(_configuration[$"{section}:WorkerAuthToken"]))
            errors.Add($"{section}:WorkerAuthToken is required when {section}:Enabled is true in Hardened profile.");
    }

    private void ValidateAiSection(List<string> errors)
    {
        var url = _configuration["AiService:GrpcAddress"]
            ?? Environment.GetEnvironmentVariable("AI_SERVICE_GRPC_ADDRESS");
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (!url.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            errors.Add("AiService:GrpcAddress must use https:// in Hardened profile.");

        if (string.IsNullOrWhiteSpace(_configuration["AiService:WorkerAuthToken"]))
            errors.Add("AiService:WorkerAuthToken is required in Hardened profile when AiService:GrpcAddress is set.");
    }

    private static void RejectPlaceholder(string? value, string key, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{key} must be set in Hardened profile.");
            return;
        }

        if (value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || value.Contains("change-me", StringComparison.OrdinalIgnoreCase)
            || value.Contains("bedemo_password", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{key} must not use demo/placeholder values in Hardened profile.");
        }
    }
}
