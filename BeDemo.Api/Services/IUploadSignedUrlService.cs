namespace BeDemo.Api.Services;

/// <summary>
/// SHV2 BE-U3 — turns stored <c>/uploads/...</c> paths into time-limited HMAC URLs served by <c>UploadsController</c>.
/// </summary>
public interface IUploadSignedUrlService
{
    /// <summary>
    /// Builds an absolute signed URL when <paramref name="storedPath"/> is under <c>/uploads/</c>;
    /// passes through external <c>https://</c> URLs unchanged.
    /// </summary>
    string? ToAbsoluteSignedUrl(string? storedPath, string scheme, string host);

    /// <summary>Relative signed serve path (e.g. for tests or same-origin clients).</summary>
    string? ToSignedServePath(string? storedPath);

    /// <summary>Validates query parameters and returns the normalized stored path (e.g. <c>/uploads/avatars/...</c>).</summary>
    bool TryValidateServeRequest(string path, long expiresUnix, string signature, out string? storedPath, out string? error);
}
