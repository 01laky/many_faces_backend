namespace BeDemo.Api.Utils;

/// <summary>
/// Path traversal defenses for files written under <c>wwwroot/uploads/**</c> (SHV2 BE-U4).
/// </summary>
/// <remarks>
/// All persisted upload paths must be built through <see cref="TryResolveFileUnderWebRoot"/> so
/// <c>Path.GetFullPath</c> cannot escape the web root (e.g. via <c>../</c> in a segment).
/// </remarks>
public static class UploadPathSecurity
{
	/// <summary>
	/// Validates a single directory or file name segment (user id folder, story id, generated file name).
	/// </summary>
	public static bool IsSafePathSegment(string? segment)
	{
		if (string.IsNullOrWhiteSpace(segment))
			return false;

		if (segment.Contains("..", StringComparison.Ordinal))
			return false;

		if (segment.IndexOfAny(['/', '\\', '\0']) >= 0)
			return false;

		if (string.Equals(segment, ".", StringComparison.Ordinal) ||
			string.Equals(segment, "..", StringComparison.Ordinal))
			return false;

		return true;
	}

	/// <summary>
	/// Resolves <paramref name="fileName"/> under <c>webRoot / relativeDirectorySegments</c> and proves the result stays inside <paramref name="webRoot"/>.
	/// </summary>
	/// <param name="webRoot">Absolute or relative wwwroot path (normalized via <see cref="Path.GetFullPath(string)"/>).</param>
	/// <param name="relativeDirectorySegments">e.g. <c>uploads</c>, <c>avatars</c>, <c>{userId}</c>.</param>
	/// <param name="fileName">Final file name only (no directories).</param>
	/// <param name="fullFilePath">Absolute path safe to open for write.</param>
	/// <param name="error">Short API error when validation fails.</param>
	public static bool TryResolveFileUnderWebRoot(
		string webRoot,
		IReadOnlyList<string> relativeDirectorySegments,
		string fileName,
		out string fullFilePath,
		out string? error)
	{
		fullFilePath = string.Empty;
		error = null;

		if (!IsSafePathSegment(fileName))
		{
			error = "Invalid file name";
			return false;
		}

		foreach (var segment in relativeDirectorySegments)
		{
			if (!IsSafePathSegment(segment))
			{
				error = "Invalid upload path";
				return false;
			}
		}

		var rootFull = Path.GetFullPath(webRoot);
		var dirParts = new string[relativeDirectorySegments.Count + 1];
		dirParts[0] = rootFull;
		for (var i = 0; i < relativeDirectorySegments.Count; i++)
			dirParts[i + 1] = relativeDirectorySegments[i];

		var directoryFull = Path.GetFullPath(Path.Combine(dirParts));
		if (!IsDescendantDirectory(rootFull, directoryFull))
		{
			error = "Invalid upload path";
			return false;
		}

		fullFilePath = Path.GetFullPath(Path.Combine(directoryFull, fileName));
		if (!IsDescendantDirectory(directoryFull, fullFilePath))
		{
			error = "Invalid upload path";
			return false;
		}

		return true;
	}

	/// <summary>
	/// Builds the public URL path segment returned to clients (always forward slashes, no traversal).
	/// </summary>
	public static string BuildUploadUrlPath(params string[] segments)
	{
		return "/" + string.Join('/', segments.Select(s => s.Replace('\\', '/')));
	}

	/// <summary>
	/// True when <paramref name="child"/> is <paramref name="parent"/> or a file/directory strictly under it.
	/// </summary>
	internal static bool IsDescendantDirectory(string parentFull, string childFull)
	{
		var comparison = OperatingSystem.IsWindows()
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;

		var parent = AppendDirectorySeparator(parentFull);
		var child = Path.GetFullPath(childFull);

		if (string.Equals(parent.TrimEnd(Path.DirectorySeparatorChar), child, comparison))
			return true;

		return child.StartsWith(parent, comparison);
	}

	private static string AppendDirectorySeparator(string path)
	{
		var full = Path.GetFullPath(path);
		if (!full.EndsWith(Path.DirectorySeparatorChar))
			full += Path.DirectorySeparatorChar;
		return full;
	}
}
