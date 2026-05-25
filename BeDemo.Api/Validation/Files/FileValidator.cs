namespace BeDemo.Api.Validation.Files;

/// <inheritdoc cref="IFileValidator" />
public sealed class FileValidator : IFileValidator
{
	private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];
	private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
	private static readonly byte[] Gif = [0x47, 0x49, 0x46];
	private static readonly byte[] Riff = [0x52, 0x49, 0x46, 0x46];

	/// <inheritdoc />
	public async Task<(bool Ok, string? ErrorCode)> ValidateImageAsync(
		Stream content,
		string fileName,
		CancellationToken cancellationToken = default)
	{
		if (content is null || !content.CanRead)
			return (false, "val_file_empty");

		var header = new byte[12];
		var read = await content.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
		if (read < 3)
			return (false, "val_file_format");

		if (StartsWith(header, Png) || StartsWith(header, Jpeg) || StartsWith(header, Gif))
			return (true, null);

		// WebP: RIFF....WEBP
		if (read >= 12 && StartsWith(header, Riff) &&
			header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
			return (true, null);

		_ = fileName;
		return (false, "val_file_format");
	}

	private static bool StartsWith(byte[] buffer, byte[] prefix)
	{
		if (buffer.Length < prefix.Length)
			return false;
		for (var i = 0; i < prefix.Length; i++)
		{
			if (buffer[i] != prefix[i])
				return false;
		}

		return true;
	}
}
