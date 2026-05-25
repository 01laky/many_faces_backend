namespace BeDemo.Api.Services;

/// <summary>Env/bootstrap Firebase service account path for seeding operator push settings on first read.</summary>
public sealed class PushFirebaseBootstrapOptions
{
	/// <summary>Readable path inside the API container for bootstrap JSON import (optional).</summary>
	public string? ServiceAccountPath { get; set; }
}
