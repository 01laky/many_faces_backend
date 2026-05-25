using System.Text.Json;

namespace BeDemo.Api.Services.OperatorPush;

internal static class FirebaseServiceAccountValidator
{
    private const int MaxJsonBytes = 32 * 1024;

    internal static bool TryValidate(string json, out string? projectId, out string? error)
    {
        projectId = null;
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Service account JSON is required.";
            return false;
        }

        if (json.Length > MaxJsonBytes)
        {
            error = "Service account JSON exceeds size limit.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp) ||
                !string.Equals(typeProp.GetString(), "service_account", StringComparison.Ordinal))
            {
                error = "type must be service_account.";
                return false;
            }

            if (!root.TryGetProperty("project_id", out var projectProp) ||
                string.IsNullOrWhiteSpace(projectProp.GetString()))
            {
                error = "project_id is required.";
                return false;
            }

            if (!root.TryGetProperty("private_key", out var keyProp) ||
                string.IsNullOrWhiteSpace(keyProp.GetString()) ||
                !keyProp.GetString()!.Contains("BEGIN", StringComparison.Ordinal))
            {
                error = "private_key is invalid or truncated.";
                return false;
            }

            if (!root.TryGetProperty("client_email", out var emailProp) ||
                string.IsNullOrWhiteSpace(emailProp.GetString()))
            {
                error = "client_email is required.";
                return false;
            }

            projectId = projectProp.GetString()!.Trim();
            return true;
        }
        catch (JsonException)
        {
            error = "Invalid JSON.";
            return false;
        }
    }
}
