namespace BeDemo.Api.Services;

public interface IProfileDetailTemplatePagesService
{
    /// <summary>Idempotent: ensures each face has exactly one profileDetail template page.</summary>
    Task<int> EnsureAllFacesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates the template page for one face when missing.</summary>
    Task<bool> EnsureForFaceAsync(int faceId, CancellationToken cancellationToken = default);

    Task<int?> GetProfileDetailPageTypeIdAsync(CancellationToken cancellationToken = default);

    /// <returns>null when valid; otherwise error message for 400.</returns>
    string? ValidateGridSchemaJson(string? gridSchemaJson);
}
