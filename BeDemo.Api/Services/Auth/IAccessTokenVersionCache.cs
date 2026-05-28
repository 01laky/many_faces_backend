namespace BeDemo.Api.Services.Auth;

/// <summary>BE-RP1 — cache <c>ApplicationUser.AccessTokenVersion</c> to avoid per-request Identity lookup.</summary>
public interface IAccessTokenVersionCache
{
	/// <summary>Returns DB version; uses memory cache when fresh.</summary>
	Task<int?> GetVersionAsync(string userId, CancellationToken cancellationToken = default);

	/// <summary>Drop cached version after session invalidation.</summary>
	void Invalidate(string userId);
}
