namespace BeDemo.Api.Validation.Files;

/// <summary>
/// Magic-byte / format validation for uploaded images (SHV2 BE-U1 + endpoint-schema-validation §7.1).
/// </summary>
public interface IFileValidator
{
	/// <summary>Validates image stream format (PNG/JPEG/GIF/WebP magic bytes).</summary>
	Task<(bool Ok, string? ErrorCode)> ValidateImageAsync(
		Stream content,
		string fileName,
		CancellationToken cancellationToken = default);
}
